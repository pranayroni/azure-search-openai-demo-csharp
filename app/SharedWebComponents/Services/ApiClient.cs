﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
// using Shared.Models;

namespace SharedWebComponents.Services;

public sealed class ApiClient(HttpClient httpClient)
{
    public async Task<ImageResponse?> RequestImageAsync(PromptRequest request)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/images", request, SerializerOptions.Default);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImageResponse>();
    }

    public async Task RequestDeleteBlobsAsync(
        DeleteRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/delete/blobs", request, SerializerOptions.Default, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RequestDeleteEmbeddingsAsync(
        DeleteRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/delete/embeddings", request, SerializerOptions.Default, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ShowLogoutButtonAsync()
    {
        var response = await httpClient.GetAsync("api/enableLogout");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<bool>();
    }

    public async Task<UploadDocumentsResponse> UploadDocumentsAsync(
        IReadOnlyList<IBrowserFile> files,
        long maxAllowedSize,
        string cookie, string category,
        CancellationToken cancellationToken)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
                // max allow size: 10mb
                var max_size = maxAllowedSize * 1024 * 1024;
#pragma warning disable CA2000 // Dispose objects before losing scope
                var fileContent = new StreamContent(file.OpenReadStream(max_size));
#pragma warning restore CA2000 // Dispose objects before losing scope
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, file.Name, file.Name);
            }

            var stringContent = new StreamContent(files[0].OpenReadStream(maxAllowedSize * 1024 * 1024));
            content.Add(stringContent, category, category);

            // set cookie
            content.Headers.Add("X-CSRF-TOKEN-FORM", cookie);
            content.Headers.Add("X-CSRF-TOKEN-HEADER", cookie);


            Console.WriteLine("Contents prepared, going to post");

            //COME BACK TO THIS
            //string json = JsonSerializer.Serialize(request);
            //var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response =
                await httpClient.PostAsync("api/documents", content, cancellationToken);


            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<UploadDocumentsResponse>();

            return result
                ?? UploadDocumentsResponse.FromError(
                    "Unable to upload files, unknown error.");
        }
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
    }

    public async IAsyncEnumerable<DocumentResponse> GetDocumentsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        httpClient.Timeout = Timeout.InfiniteTimeSpan;

        var response = await httpClient.GetAsync("api/documents", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var document in
                JsonSerializer.DeserializeAsyncEnumerable<DocumentResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async Task<List<string>> GetCategoriesAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await httpClient.GetAsync("api/categories", cancellationToken);

        Console.WriteLine(response);

        if (response.IsSuccessStatusCode)
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var categories = JsonSerializer.Deserialize<List<string>>(jsonResponse);
            return categories;
        }
        else
        {
            // Handle error or throw an exception
            throw new HttpRequestException($"Failed to fetch categories. Status code: {response.StatusCode}");
        }

    }

    public Task<AnswerResult<ChatRequest>> ChatConversationAsync(ChatRequest request)
    {

        Console.WriteLine("Entered Here...");
        return PostRequestAsync(request, "api/chat");
    }

    private async Task<AnswerResult<TRequest>> PostRequestAsync<TRequest>(
        TRequest request, string apiRoute) where TRequest : ApproachRequest
    {
        var result = new AnswerResult<TRequest>(
            IsSuccessful: false,
            Response: null,
            Approach: request.Approach,
            Request: request);

        var json = JsonSerializer.Serialize(
            request,
            SerializerOptions.Default);

        using var body = new StringContent(
            json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(apiRoute, body);

        if (response.IsSuccessStatusCode)
        {
            var answer = await response.Content.ReadFromJsonAsync<ChatAppResponseOrError>();
            return result with
            {
                IsSuccessful = answer is not null,
                Response = answer,
            };
        }
        else
        {
            var errorTitle = $"HTTP {(int)response.StatusCode} : {response.ReasonPhrase ?? "☹️ Unknown error..."}";
            var answer = new ChatAppResponseOrError(
                Array.Empty<ResponseChoice>(),
                errorTitle);

            return result with
            {
                IsSuccessful = false,
                Response = answer
            };
        }
    }
}
