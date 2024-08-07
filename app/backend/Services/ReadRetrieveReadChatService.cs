﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Azure.Core;
using Humanizer;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using SharedWebComponents.Components;
using SharedWebComponents.Pages;
using static MudBlazor.CategoryTypes;

namespace MinimalApi.Services;
#pragma warning disable SKEXP0011 // Mark members as static
#pragma warning disable SKEXP0001 // Mark members as static
public class ReadRetrieveReadChatService
{
    private readonly ISearchService _searchClient;
    private readonly Kernel _kernel;
    private readonly IConfiguration _configuration;
    private readonly IComputerVisionService? _visionService;
    private readonly TokenCredential? _tokenCredential;
    private readonly OpenAIClient _openAIClient;

    public ReadRetrieveReadChatService(
        ISearchService searchClient,
        OpenAIClient client,
        IConfiguration configuration,
        IComputerVisionService? visionService = null,
        TokenCredential? tokenCredential = null)
    {
        _searchClient = searchClient;
        _openAIClient = client;
        var kernelBuilder = Kernel.CreateBuilder();

        bool useAzureOpenAI = (configuration["UseAOAI"] == "true");
        useAzureOpenAI = true;

        if (!useAzureOpenAI)
        {
            var deployment = configuration["OpenAiChatGptDeployment"];
            ArgumentNullException.ThrowIfNullOrWhiteSpace(deployment);
            kernelBuilder = kernelBuilder.AddOpenAIChatCompletion(deployment, client);

            var embeddingModelName = configuration["OpenAiEmbeddingDeployment"];
            ArgumentNullException.ThrowIfNullOrWhiteSpace(embeddingModelName);
            kernelBuilder = kernelBuilder.AddOpenAITextEmbeddingGeneration(embeddingModelName, client);
        }
        else
        {
            //var deployedModelName = configuration["AzureOpenAiChatGptDeployment"];
            var deployedModelName = "chat4o";
            ArgumentNullException.ThrowIfNullOrWhiteSpace(deployedModelName);
            //var embeddingModelName = configuration["AzureOpenAiEmbeddingDeployment"];
            var embeddingModelName = "embedding";
            if (!string.IsNullOrEmpty(embeddingModelName))
            {
                var endpoint = configuration["AzureOpenAiServiceEndpoint"];
                ArgumentNullException.ThrowIfNullOrWhiteSpace(endpoint);
                kernelBuilder = kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(embeddingModelName, endpoint, tokenCredential ?? new DefaultAzureCredential());
                kernelBuilder = kernelBuilder.AddAzureOpenAIChatCompletion(deployedModelName, endpoint, tokenCredential ?? new DefaultAzureCredential());
            }
        }

        _kernel = kernelBuilder.Build();
        _configuration = configuration;
        _visionService = visionService;
        _tokenCredential = tokenCredential;
    }

    public async Task<ChatAppResponse> ReplyAsync(
        ChatMessage[] history,
        RequestOverrides? overrides,
        CancellationToken cancellationToken = default)
    {
        var dateTime = DateTime.Now;
        var year = dateTime.Year;

        var top = overrides?.Top ?? 3;
        var useSemanticCaptions = overrides?.SemanticCaptions ?? false;
        var useSemanticRanker = overrides?.SemanticRanker ?? false;
        var exclude_category_ienum = overrides?.ExcludeCategory;
        var filter = string.Empty;

        if (exclude_category_ienum != null)
        {
            var exclude_category = exclude_category_ienum.ToList();
            if (exclude_category != null && exclude_category.Count > 0)
            {
                var categoryFilters = exclude_category.Select(cat => $"not category/any(c: c eq '{cat}')");
                filter = string.Join(" and ", categoryFilters);
            }
        }
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var embedding = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        float[]? embeddings = null;
        var question = history.LastOrDefault(m => m.IsUser)?.Content is { } userQuestion
            ? userQuestion.Replace("knipper", string.Empty)
            : throw new InvalidOperationException("Use question is null");

        question += " " + year;

        string[]? followUpQuestionList = null;
        if (overrides?.RetrievalMode != RetrievalMode.Text && embedding is not null)
        {
            embeddings = (await embedding.GenerateEmbeddingAsync(question, cancellationToken: cancellationToken)).ToArray();
        }

        // step 1
        // use llm to get query if retrieval mode is not vector
        string? query = null;
        if (overrides?.RetrievalMode != RetrievalMode.Vector)
        {
            var getQueryChat = new ChatHistory("You are a helpful AI assistant, generate search query for following question. " +
                "Make your response simple and precise. Return the query only, do not return any other text. " +
                "Your query should include the keywords Knipper and {year} if the question is related to time. " +
                "e.g. Knipper {year} holiday schedule. " +
                "e.g. Knipper AOC rules Agile. e.g. Knipper Health Plus AND standard plan.");

            getQueryChat.AddUserMessage(question);
            var result = await chat.GetChatMessageContentAsync(
                getQueryChat,
                cancellationToken: cancellationToken);

            query = result.Content ?? question;
            Console.WriteLine("query = " + query);

        }

        // step 2
        // use query to search related docs
        var documentContentList = await _searchClient.QueryDocumentsAsync(query, embeddings, overrides, cancellationToken);

        string documentContents = string.Empty;
        if (documentContentList.Length == 0)
        {
            documentContents = "no source available.";
        }
        else
        {
            documentContents = string.Join("\r", documentContentList.Select(x =>$"{x.Title}:{x.Content}"));
        }

        // step 2.5
        // retrieve images if _visionService is available
        SupportingImageRecord[]? images = default;
        if (_visionService is not null)
        {
            var queryEmbeddings = await _visionService.VectorizeTextAsync(query ?? question, cancellationToken);
            images = await _searchClient.QueryImagesAsync(query, queryEmbeddings.vector, overrides, cancellationToken);
        }

        // step 3
        // put together related docs and conversation history to generate answer
        var answerChat = new ChatHistory(
            "You are an AI assistant helps Knipper employees with questions about the employee handbook " +
            "and other documents such as Standard Operation Procedures and business rules with client companies. " +
            "Answer only with the facts listed in the provided sources. " +
            "If you cannot find the answer in the sources, reply with 'I don't know.'. " +
            "If asking a clarifying question to the user would help, ask the question. " +
            "For tabular information return it as an html table. Do not return markdown format. " +
            "If the question is not in English, answer in the language used in the question.");

        // add chat history
        foreach (var message in history)
        {
            if (message.IsUser)
            {
                answerChat.AddUserMessage(message.Content);
            }
            else
            {
                answerChat.AddAssistantMessage(message.Content);
            }
        }
        if (images != null)
        {
            var prompt = @$"## Source ##

            {documentContents}
## End ##

Answer question based on available sources and images.
Your answer needs to be a json object with answer and thoughts field.
Don't put your answer between ```json and ```, return the json string directly. e.g {{""answer"": ""I don't know"", ""thoughts"": ""I don't know""}}
            ";

            var tokenRequestContext = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
            var sasToken = await (_tokenCredential?.GetTokenAsync(tokenRequestContext, cancellationToken) ?? throw new InvalidOperationException("Failed to get token"));
            var sasTokenString = sasToken.Token;
            var imageUrls = images.Select(x => $"{x.Url}?{sasTokenString}").ToArray();
            var collection = new ChatMessageContentItemCollection();
            collection.Add(new TextContent(prompt));
            foreach (var imageUrl in imageUrls)
            {
                collection.Add(new ImageContent(new Uri(imageUrl)));
            }

            answerChat.AddUserMessage(collection);
        }
        else
        {
            var prompt = @$" ## Source ##
{documentContents}
## End ##

You answer needs to be a json object with the following format.
{{
    ""answer"": // the answer to the question, add a source reference to the end of each sentence. e.g. Apple is a fruit [reference1.pdf][reference2.pdf]. If no source available, put the answer as 'I don't know.'
    ""thoughts"": // brief thoughts on how you came up with the answer, e.g. what sources you used, what you thought about, etc.
}}";
            answerChat.AddUserMessage(prompt);
        }

        var promptExecutingSetting = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 1024,
            Temperature = overrides?.Temperature ?? 0.7,
            StopSequences = [],
        };

        // get answer
        var answer = await chat.GetChatMessageContentAsync(
                       answerChat,
                       promptExecutingSetting,
                       cancellationToken: cancellationToken);
        var answerJson = answer.Content ?? throw new InvalidOperationException("Failed to get search query");
        var ans = answerJson;
        var thoughts = string.Empty;
        if (answerJson[0] == '{')
        {
            var answerObject = JsonSerializer.Deserialize<JsonElement>(answerJson);
            ans = answerObject.GetProperty("answer").GetString() ?? throw new InvalidOperationException("Failed to get answer");
            thoughts = answerObject.GetProperty("thoughts").GetString() ?? throw new InvalidOperationException("Failed to get thoughts");
            Console.WriteLine("ans = "+ans);
            if(ans == "I don't know.")
            {
                Console.WriteLine("Entered I don't know branch");
                var deploymentId = "chat4o";
                var chatCompletionsOptions = GetChatCompletionsOptions(deploymentId, history);
                var response = await _openAIClient.GetChatCompletionsStreamingAsync(
                    chatCompletionsOptions, cancellationToken);

                StringBuilder compiledResponse = new StringBuilder();
                await foreach (var choices in response.WithCancellation(cancellationToken))
                {
                    if (choices.ContentUpdate is { Length: > 0 })
                    {
                        compiledResponse.Append(choices.ContentUpdate);
                    }
                }
                ans = compiledResponse.ToString();
                // Optionally, log the compiled response for debugging
                Console.WriteLine("Compiled Response: " + ans);
            }
        }

        // step 4
        // add follow up questions if requested
        if (overrides?.SuggestFollowupQuestions is true)
        {
            var followUpQuestionChat = new ChatHistory(@"You are a helpful AI assistant");
            followUpQuestionChat.AddUserMessage($@"Generate three follow-up question the user may ask based on the answer you just generated.
# Answer
{ans}

# Format of the response
Return the follow-up question as a json string list. Don't put your answer between ```json and ```, return the json string directly.
e.g.
[
    ""What is the deductible?"",
    ""What is the co-pay?"",
    ""What is the out-of-pocket maximum?""
]");

            var followUpQuestions = await chat.GetChatMessageContentAsync(
                followUpQuestionChat,
                promptExecutingSetting,
                cancellationToken: cancellationToken);

            var followUpQuestionsJson = followUpQuestions.Content ?? throw new InvalidOperationException("Failed to get search query");
            var followUpQuestionsObject = JsonSerializer.Deserialize<JsonElement>(followUpQuestionsJson);
            var followUpQuestionsList = followUpQuestionsObject.EnumerateArray().Select(x => x.GetString()!).ToList();
            foreach (var followUpQuestion in followUpQuestionsList)
            {
                ans += $" <<{followUpQuestion}>> ";
            }

            followUpQuestionList = followUpQuestionsList.ToArray();
        }

        var responseMessage = new ResponseMessage("assistant", ans);
        var responseContext = new ResponseContext(
            DataPointsContent: documentContentList.Select(x => new SupportingContentRecord(x.Title, x.Content)).ToArray(),
            DataPointsImages: images?.Select(x => new SupportingImageRecord(x.Title, x.Url)).ToArray(),
            FollowupQuestions: followUpQuestionList ?? Array.Empty<string>(),
            Thoughts: new[] { new Thoughts("Thoughts", thoughts) });

        var choice = new ResponseChoice(
            Index: 0,
            Message: responseMessage,
            Context: responseContext,
            CitationBaseUrl: _configuration.ToCitationBaseUrl());

        return new ChatAppResponse(new[] { choice });
    }
    private ChatCompletionsOptions GetChatCompletionsOptions(string deploymentId, ChatMessage[] history)
    {
        var dateTime = DateTime.Now;
        var date = dateTime.Date;
        ChatCompletionsOptions chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = deploymentId,
            Messages = { new ChatRequestSystemMessage(
                "You are a system assistant who helps the company employees with their questions. " +
                "Be precise and detailed in your answer. The current date is {date}" +
                "Start your message with 'I could not find an answer in the provided documents. According to the internet, ' " +
                "and then answer the question based on general knowlegde or the internet. " +
                "If asking a clarifying question to the user would help, ask the question. " +
                "If the question is not in English, answer in the language used in the question.")
            }
        };

        foreach (var message in history)
        {
            if (message.IsUser)
            {

                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message.Content));
            }
            else
            {
                chatCompletionsOptions.Messages.Add(new ChatRequestAssistantMessage(message.Content));
            }
        }

        return chatCompletionsOptions;

    }
}
