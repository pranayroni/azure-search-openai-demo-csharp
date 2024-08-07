﻿// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder.Extensions;
using System.IO;

namespace MinimalApi.Services;

internal sealed class AzureBlobStorageService(BlobContainerClient container)
{
    internal static DefaultAzureCredential DefaultCredential { get; } = new();

    internal async Task<UploadDocumentsResponse> UploadFilesAsync(IEnumerable<IFormFile> files, string[] category, CancellationToken cancellationToken)
    {
        try
        {

            /*string openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? string.Empty;
            string embeddingDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT") ?? string.Empty;
            string searchServiceEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_SERVICE_ENDPOINT") ?? string.Empty;
            string searchIndex = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX") ?? string.Empty;
            string formRecognizerServiceEndpoint = Environment.GetEnvironmentVariable("AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT") ?? string.Empty;
            string storageBlobEndpoint = Environment.GetEnvironmentVariable("AZURE_STORAGE_BLOB_ENDPOINT") ?? string.Empty;*/

            string openAiEndpoint = "https://docbot-aoai.openai.azure.com/";
            string embeddingDeployment = "embedding";
            string searchServiceEndpoint = "https://gptkb-r6lomx22dqabk.search.windows.net/";
            string searchIndex = "gptkbindex";
            string formRecognizerServiceEndpoint = "https://cog-fr-r6lomx22dqabk.cognitiveservices.azure.com/";
            string storageBlobEndpoint = "https://str6lomx22dqabk.blob.core.windows.net/";

            DefaultAzureCredential defaultCredential = new DefaultAzureCredential();
            var embedService = new AzureSearchEmbedService(
                openAIClient: new OpenAIClient(
                    new Uri(openAiEndpoint),
                    defaultCredential),
                embeddingModelName: embeddingDeployment,
                searchClient: new SearchClient(
                    new Uri(searchServiceEndpoint),
                    searchIndex,
                    defaultCredential),
                searchIndexName: searchIndex,
                searchIndexClient: new SearchIndexClient(
                    new Uri(searchServiceEndpoint),
                    defaultCredential),
                documentAnalysisClient: new DocumentAnalysisClient(
                    new Uri(formRecognizerServiceEndpoint),
                    defaultCredential,
                    new DocumentAnalysisClientOptions
                    {
                        Diagnostics =
                        {
                            IsLoggingContentEnabled = true
                        }
                    }),
                corpusContainerClient: new BlobServiceClient(
                    new Uri(storageBlobEndpoint),
                    defaultCredential).GetBlobContainerClient("corpus")
            );

            List<string> uploadedFiles = [];
            foreach (var file in files)
            {
                var fileName = file.FileName;

                await using var stream = file.OpenReadStream();

                // if file is an image (end with .png, .jpg, .jpeg, .gif), upload it to blob storage
                if (Path.GetExtension(fileName).ToLower() is ".png" or ".jpg" or ".jpeg" or ".gif")
                {
                    var blobName = BlobNameFromFilePage(fileName);
                    var blobClient = container.GetBlobClient(blobName);
                    if (await blobClient.ExistsAsync(cancellationToken))
                    {
                        continue;
                    }

                    var url = blobClient.Uri.AbsoluteUri;
                    await using var fileStream = file.OpenReadStream();
                    await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
                    {
                        ContentType = "image"
                    }, cancellationToken: cancellationToken);
                    uploadedFiles.Add(blobName);
                    // revert stream position
                    stream.Position = 0;
                    await embedService.EmbedPDFBlobAsync(fileStream, blobName, fileName, category);
                }
                else if (Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    using var documents = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < documents.PageCount; i++)
                    {
                        var documentName = BlobNameFromFilePage(fileName, i);
                        var blobClient = container.GetBlobClient(documentName);
                        if (await blobClient.ExistsAsync())
                        {
                            continue;
                        }

                        var tempFileName = Path.GetTempFileName();

                        try
                        {
                            using var document = new PdfDocument();
                            document.AddPage(documents.Pages[i]);
                            document.Save(tempFileName);

                            await using var tempStream = File.OpenRead(tempFileName);
                            await blobClient.UploadAsync(tempStream, new BlobHttpHeaders
                            {
                                ContentType = "application/pdf"
                            });

                            uploadedFiles.Add(documentName);
                            // revert stream position
                            stream.Position = 0;
                            tempStream.Position = 0;
                            await embedService.EmbedPDFBlobAsync(tempStream, documentName, fileName, category);
                        }
                        finally
                        {
                            File.Delete(tempFileName);
                        }
                    }
                }
            }

            if (uploadedFiles.Count is 0)
            {
                return UploadDocumentsResponse.FromError("""
                    No files were uploaded. Either the files already exist or the files are not PDFs or images.
                    """
                );
            }
            

            return new UploadDocumentsResponse([.. uploadedFiles]);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static string BlobNameFromFilePage(string filename, int page = 0) =>
        Path.GetExtension(filename).ToLower() is ".pdf"
            ? $"{Path.GetFileNameWithoutExtension(filename)}-{page}.pdf"
            : Path.GetFileName(filename);
}
