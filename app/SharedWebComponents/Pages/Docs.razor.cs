﻿// Copyright (c) Microsoft. All rights reserved.

using MinimalApi.Models;

namespace SharedWebComponents.Pages;

public sealed partial class Docs : IDisposable
{
    private const long MaxIndividualFileSize = 1_024L * 1_024;

    private MudForm _form = null!;
    private MudFileUpload<IReadOnlyList<IBrowserFile>> _fileUpload = null!;
    private Task _getDocumentsTask = null!;
    private bool _isLoadingDocuments = false;
    private bool _isUploadingDocuments = false;
    private string _filter = "";

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<DocumentResponse> _documents = [];

    public string category = "";

    [Inject]
    public required ApiClient Client { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required ILogger<Docs> Logger { get; set; }

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    [Inject]
    public required IPdfViewer PdfViewer { get; set; }

    private bool FilesSelected => _fileUpload is { Files.Count: > 0 };

    protected override void OnInitialized() =>
        // Instead of awaiting this async enumerable here, let's capture it in a task
        // and start it in the background. This way, we can await it in the UI.
        _getDocumentsTask = GetDocumentsAsync();

    private bool OnFilter(DocumentResponse document) => document is not null
        && (string.IsNullOrWhiteSpace(_filter) || document.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase));

    private async Task GetDocumentsAsync()
    {
        _isLoadingDocuments = true;

        try
        {
            var documents =
                await Client.GetDocumentsAsync(_cancellationTokenSource.Token)
                    .ToListAsync();

            foreach (var document in documents)
            {
                _documents.Add(document);
            }
        }
        finally
        {
            _isLoadingDocuments = false;
            StateHasChanged();
        }
    }

    private async Task SubmitFilesForUploadAsync()
    {
        _isUploadingDocuments = true;
        if (_fileUpload is { Files.Count: > 0 })
        {
            Snackbar.Add(
                $"Uploading documents. " +
                $"This may take a couple of minutes, please be patient. " +
                $"You will not be able to upload additional documents during this time.",
                Severity.Success,
                static options =>
                {
                    options.ShowCloseIcon = true;
                });
            var cookie = await JSRuntime.InvokeAsync<string>("getCookie", "XSRF-TOKEN");
            
            var result = await Client.UploadDocumentsAsync(
                _fileUpload.Files, MaxIndividualFileSize, cookie, category);

            Logger.LogInformation("Result: {x}", result);

            if (result.IsSuccessful)
            {
                manager.NavigateTo(manager.Uri, true);
                Snackbar.Add(
                    $"Uploaded {result.UploadedFiles.Length} documents.",
                    Severity.Success,
                    static options =>
                    {
                        options.ShowCloseIcon = true;
                        options.VisibleStateDuration = 10_000;
                    });
                _isUploadingDocuments = false;
                await _fileUpload.ResetAsync();
            }
            else
            {
                Snackbar.Add(
                    result.Error,
                    Severity.Error,
                    static options =>
                    {
                        options.ShowCloseIcon = true;
                        options.VisibleStateDuration = 10_000;
                    });
            }
        }
        _isUploadingDocuments = false;
    }

    private ValueTask OnShowDocumentAsync(DocumentResponse document) =>
        PdfViewer.ShowDocumentAsync(document.Name, document.Url.ToString());

    private async ValueTask OnDeleteAsync(NavigationManager manager, string fileName)
    {
        var index = fileName.LastIndexOf("-");
        fileName = fileName.Substring(0, index) + ".pdf";
        Snackbar.Add(
             $"Deleting {fileName}.",
             Severity.Success,
             static options =>
             {
                 options.ShowCloseIcon = true;
                 options.VisibleStateDuration = 10_000;
             });
        DeleteRequest deleteRequest = new()
        {
            file = fileName
        };
        await Client.RequestDeleteAsync(deleteRequest);
        manager.NavigateTo(manager.Uri, true);

    }

    public void Dispose() => _cancellationTokenSource.Cancel();
}
