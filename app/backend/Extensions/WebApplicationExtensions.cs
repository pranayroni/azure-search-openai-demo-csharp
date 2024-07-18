// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing;
using Shared.Models;
using Azure;
using Microsoft.AspNetCore.Builder.Extensions;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;


namespace MinimalApi.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication MapApi(this WebApplication app)
    {
        // ** This is where you define your API endpoints **
        Console.WriteLine("Mapping API endpoints");
        var api = app.MapGroup("api");

        // Blazor 📎 Clippy streaming endpoint
        api.MapPost("openai/chat", OnPostChatPromptAsync);

        // Long-form chat w/ contextual history endpoint
        api.MapPost("chat", OnPostChatAsync);

        // Upload a document
        api.MapPost("documents", OnPostDocumentAsync).DisableAntiforgery();

        // Get all documents
        api.MapGet("documents", OnGetDocumentsAsync);

        // Get DALL-E image result from prompt
        api.MapPost("images", OnPostImagePromptAsync);

        api.MapGet("username", OnGetUsername).RequireAuthorization();

        api.MapGet("enableLogout", OnGetEnableLogout);

        api.MapGet("categories", OnGetCategoriesAsync);

        api.MapPost("delete", OnPostDeleteAsync);


        return app;
    }

    [AllowAnonymous]
    private static IResult OnGetUsername(HttpContext httpContext)
    {
        if (httpContext.User.Identity.IsAuthenticated)
        {
            var preferredUsername = httpContext.User.FindFirst("preferred_username")?.Value ?? "Unknown";
            var email = httpContext.User.FindFirst("email")?.Value ?? "Unknown";
            return Results.Ok(new {PreferredUsername = preferredUsername, Email = email});
        } else
        {
            Console.WriteLine("User was unauthenticated.");
            return Results.Unauthorized();
        }
    }
    private static async Task<IResult> OnGetCategoriesAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var requestUri = new Uri("https://gptkb-r6lomx22dqabk.search.windows.net/indexes/gptkbindex/docs?api-version=2024-05-01-preview&facet=category,count:1000");
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("api-key", "PQy5AIQF6dO3Ng2Pi15mgFIHsv5A3cc4XQOOoDIqIwAzSeA6WCrs");

        try
        {
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var categories = JObject.Parse(jsonResponse)["@search.facets"]["category"]
                    .Select(c => c["value"].ToString())
                    .ToList();

                return Results.Json(categories);
            }
            else
            {
                // Log the error or handle it as needed
                return Results.Problem($"Failed to fetch categories. Status code: {response.StatusCode}", statusCode: (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Log the exception details as needed
            return Results.Problem($"Error fetching categories: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }



    private static IResult OnGetEnableLogout(HttpContext context)
    {
        var header = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"];
        var enableLogout = !string.IsNullOrEmpty(header);

        return TypedResults.Ok(enableLogout);
    }

    private static async IAsyncEnumerable<ChatChunkResponse> OnPostChatPromptAsync(
        PromptRequest prompt,
        OpenAIClient client,
        IConfiguration config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        //var deploymentId = config["AZURE_OPENAI_CHATGPT_DEPLOYMENT"];
        var deploymentId = "chat4o";
        var response = await client.GetChatCompletionsStreamingAsync(
            new ChatCompletionsOptions
            {
                DeploymentName = deploymentId,
                Messages =
                {
                    new ChatRequestSystemMessage("""
                        You're an AI assistant for developers, helping them write code more efficiently.
                        You're name is **Blazor 📎 Clippy** and you're an expert Blazor developer.
                        You're also an expert in ASP.NET Core, C#, TypeScript, and even JavaScript.
                        You will always reply with a Markdown formatted response.
                        """),
                    new ChatRequestUserMessage("What's your name?"),
                    new ChatRequestAssistantMessage("Hi, my name is **Blazor 📎 Clippy**! Nice to meet you."),
                    new ChatRequestUserMessage(prompt.Prompt)
                }
            }, cancellationToken);
        
        await foreach (var choice in response.WithCancellation(cancellationToken))
        {
            if (choice.ContentUpdate is { Length: > 0 })
            {
                yield return new ChatChunkResponse(choice.ContentUpdate.Length, choice.ContentUpdate);
            }
        }
        Console.WriteLine("Prompt: "+ prompt.Prompt);
        await Task.Delay(1);

    }

    private static async Task<IResult> OnPostChatAsync(
        ChatRequest request,
        ReadRetrieveReadChatService chatService,
        CancellationToken cancellationToken)
    {
        if (request is { History.Length: > 0 })
        {
            var response = await chatService.ReplyAsync(
                request.History, request.Overrides, cancellationToken);

            return TypedResults.Ok(response);
        }

        return Results.BadRequest();
    }

    private static async Task<IResult> OnPostDocumentAsync(
        [FromForm] IFormFileCollection files,
        [FromServices] AzureBlobStorageService service,
        [FromServices] ILogger<AzureBlobStorageService> logger,
        CancellationToken cancellationToken)
    {

        logger.LogInformation("Upload documents");
        Console.WriteLine("Upload documents");


        var filesList = files.ToList();
        var content = new List<IFormFile>();
        for(int i = 0; i < filesList.Count-1; i++){
            content.Add(filesList[i]);
        }
        var category = filesList[filesList.Count-1].FileName.Split(',');
        
        var response = await service.UploadFilesAsync(content, category, cancellationToken);

        logger.LogInformation("Upload documents: {x}", response);

        return TypedResults.Ok(response);
    }

    private static async IAsyncEnumerable<DocumentResponse> OnGetDocumentsAsync(
        BlobContainerClient client,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Console.WriteLine("Got Document Request");
        await foreach (var blob in client.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob is not null and { Deleted: false })
            {
                var props = blob.Properties;
                var baseUri = client.Uri;
                var builder = new UriBuilder(baseUri);
                builder.Path += $"/{blob.Name}";

                var metadata = blob.Metadata;
                var documentProcessingStatus = GetMetadataEnumOrDefault<DocumentProcessingStatus>(
                    metadata, nameof(DocumentProcessingStatus), DocumentProcessingStatus.NotProcessed);
                var embeddingType = GetMetadataEnumOrDefault<EmbeddingType>(
                    metadata, nameof(EmbeddingType), EmbeddingType.AzureSearch);

                yield return new(
                    blob.Name,
                    props.ContentType,
                    props.ContentLength ?? 0,
                    props.LastModified,
                    builder.Uri,
                    documentProcessingStatus,
                    embeddingType);

                static TEnum GetMetadataEnumOrDefault<TEnum>(
                    IDictionary<string, string> metadata,
                    string key,
                    TEnum @default) where TEnum : struct => metadata.TryGetValue(key, out var value)
                        && Enum.TryParse<TEnum>(value, out var status)
                            ? status
                            : @default;
            }
        }
    }

    private static async Task<IResult> OnPostDeleteAsync(
        DeleteRequest deleteRequest,
        CancellationToken cancellationToken
    )
    {
        var file = deleteRequest.file;

        Console.WriteLine($"Trying to delete {file}");
        await RemoveBlobsAsync(file);
        await RemoveFromIndexAsync(file);

        return TypedResults.Ok();
    }

    private static async ValueTask RemoveBlobsAsync(string fileName)
    {
        Console.WriteLine($"Removing blobs for '{fileName ?? "all"}'");


        var prefix = fileName == null ? "": fileName.Split(".pdf").First();

        Console.WriteLine("prefix: " + prefix);

        DefaultAzureCredential defaultCredential = new DefaultAzureCredential();
        string storageBlobEndpoint = "https://str6lomx22dqabk.blob.core.windows.net/";

        var blobService = new BlobServiceClient(
            new Uri(storageBlobEndpoint),
            defaultCredential);

        var getContainerClient = blobService.GetBlobContainerClient("content");
        var getCorpusClient = blobService.GetBlobContainerClient("corpus");
        var clients = new[] { getContainerClient, getCorpusClient };

        foreach (var client in clients)
        {
            await DeleteAllBlobsFromContainerAsync(client, prefix);
        }

        static async Task DeleteAllBlobsFromContainerAsync(BlobContainerClient client, string? prefix)
        {
            await foreach (var blob in client.GetBlobsAsync())
            {
                if (string.IsNullOrWhiteSpace(prefix) ||
                    blob.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    await client.DeleteBlobAsync(blob.Name);
                }
            }
        };
    }

    private static async ValueTask RemoveFromIndexAsync(string fileName)
    {
        var searchIndex = "gptkbindex";
        string searchServiceEndpoint = "https://gptkb-r6lomx22dqabk.search.windows.net/";
        DefaultAzureCredential defaultCredential = new DefaultAzureCredential();

        var searchClient = new SearchClient(
                new Uri(searchServiceEndpoint),
                searchIndex,
                defaultCredential);

        Console.WriteLine($"""
        Removing sections from '{fileName ?? "all"}' from search index '{searchIndex}.'
        """);

        while (true)
        {
            var filter = (fileName is null) ? null : $"sourcefile eq '{Path.GetFileName(fileName)}'";

            var response = await searchClient.SearchAsync<SearchDocument>("",
                new SearchOptions
                {
                    Filter = filter,
                    Size = 1_000,
                    IncludeTotalCount = true
                });

            var documentsToDelete = new List<SearchDocument>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                documentsToDelete.Add(new SearchDocument
                {
                    ["id"] = result.Document["id"]
                });
            }

            if (documentsToDelete.Count == 0)
            {
                break;
            }
            Response<IndexDocumentsResult> deleteResponse =
                await searchClient.DeleteDocumentsAsync(documentsToDelete);
            
            Console.WriteLine($"""
                Removed {deleteResponse.Value.Results.Count} sections from index
            """);

            // It can take a few seconds for search results to reflect changes, so wait a bit
            await Task.Delay(TimeSpan.FromMilliseconds(2_000));
        }
    }




    private static async Task<IResult> OnPostImagePromptAsync(
        PromptRequest prompt,
        OpenAIClient client,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Image Generation is disabled.");

        return TypedResults.Ok();
    }
}
