// Copyright (c) Microsoft. All rights reserved.

using Newtonsoft.Json.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SharedWebComponents.Pages;

public sealed partial class Docs : IDisposable
{
    private const long MaxIndividualFileSize = 1_024L * 1_024;

    private MudForm _form = null!;
    private MudFileUpload<IReadOnlyList<IBrowserFile>> _fileUpload = null!;
    private Task _getDocumentsTask = null!;
    private Task _getCategoriesTask = null!;
    private bool _isLoadingCategories = false;
    private bool _isLoadingDocuments = false;
    private int _deleteProgress { get; set; }
    private int _uploadProgress { get; set; }

    private string _filter = "";

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<DocumentResponse> _documents = [];

    public IEnumerable<string> category = new List<string>();
    public List<string>? cList = null;

    [CascadingParameter] public bool _isUploadingDocuments { get; set; }
    [CascadingParameter] public bool _isDeletingDocuments { get; set; }
    [CascadingParameter] public EventCallback<UploadDocumentsArgs> UploadDocumentsEvent { get; set; }

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

    protected override void OnInitialized()
    {
        // Instead of awaiting this async enumerable here, let's capture it in a task
        // and start it in the background. This way, we can await it in the UI.
        category = new List<string>();
        _getDocumentsTask = GetDocumentsAsync();
        _getCategoriesTask = GetCategoriesAsync();
    }

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

    private async Task GetCategoriesAsync()
    {
        _isLoadingCategories = true;

        try
        {
            cList = await Client.GetCategoriesAsync(_cancellationTokenSource.Token);
        }
        finally
        {
            _isLoadingCategories = false;
            StateHasChanged();
        }
    }

    private async Task SubmitFilesForUploadAsync()
    {
        //_isUploadingDocuments = true;
        // Add the beforeunload event listener at the start of the upload process
        //await JSRuntime.InvokeVoidAsync("addBeforeUnloadListener");

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

            UploadDocumentsArgs args = new()
            {
                Files = _fileUpload.Files,
                Category = string.Join(',', category),
                Success = false
            };

            await UploadDocumentsEvent.InvokeAsync(args);

            if (args.Success)
            {
                //await JSRuntime.InvokeVoidAsync("removeBeforeUnloadListener");
                manager.NavigateTo(manager.Uri, true);
                Snackbar.Add(
                    $"Uploaded {args.UploadedFilesCount} documents.",
                    Severity.Success,
                    static options =>
                    {
                        options.ShowCloseIcon = true;
                        options.VisibleStateDuration = 10_000;
                    });
                //_isUploadingDocuments = false;
                await _fileUpload.ResetAsync();
            }
            else
            {
                Snackbar.Add(
                    args.ErrorMessage,
                    Severity.Error,
                    static options =>
                    {
                        options.ShowCloseIcon = true;
                        options.VisibleStateDuration = 10_000;
                    });
                //await JSRuntime.InvokeVoidAsync("removeBeforeUnloadListener");
                //_isUploadingDocuments = false;
            }
        }
        
    }

    private ValueTask OnShowDocumentAsync(DocumentResponse document) =>
        PdfViewer.ShowDocumentAsync(document.Name, document.Url.ToString());

    private async ValueTask OnDeleteAsync(NavigationManager manager, string fileName)
    {
        _deleteProgress = 10;
        _isDeletingDocuments = true;
        await JSRuntime.InvokeVoidAsync("addBeforeUnloadListener");
        var index = fileName.LastIndexOf("-");
        fileName = fileName.Substring(0, index) + ".pdf";
        Snackbar.Add(
             $"Deleting {fileName}. " +
             $"This may take a couple of minutes, please be patient. " +
             $"You will not be able to delete other documents during this time.",
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
        var cancellationToken = _cancellationTokenSource.Token;
        await Client.RequestDeleteBlobsAsync(deleteRequest, cancellationToken);
        _deleteProgress = 50;
        StateHasChanged();
        await Client.RequestDeleteEmbeddingsAsync(deleteRequest, cancellationToken);
        _deleteProgress = 100;
        StateHasChanged();
        await Task.Delay(300);
        await JSRuntime.InvokeVoidAsync("removeBeforeUnloadListener");
        _isDeletingDocuments = false;
        manager.NavigateTo(manager.Uri, true);

    }

    public async Task<IEnumerable<string>> CustomSearchFuncAsync(string search)
    {
        if (string.IsNullOrEmpty(search))
        {
            return cList.AsEnumerable<string>();
        }
        return await Task.FromResult(cList.Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase)));
        
    }

    public void Dispose() => _cancellationTokenSource.Cancel();
    

}
