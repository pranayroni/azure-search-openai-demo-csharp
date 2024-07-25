// Copyright (c) Microsoft. All rights reserved.

using Newtonsoft.Json.Linq;
using SharedWebComponents.Shared;
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
    private int _uploadProgress { get; set; }

    private string _filter = "";

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<DocumentResponse> _documents = [];

    public IEnumerable<string> category = new List<string>();
    public List<string>? cList = null;

    [CascadingParameter] public bool _isUploadingDocuments { get; set; }
    [CascadingParameter] public EventCallback<UploadDocumentsArgs> UploadDocumentsEvent { get; set; }
    [CascadingParameter] public EventCallback<string> DeleteDocumentsEvent { get; set; }


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
    [Inject]
    public required DeleteService DeleteService { get; set; }

    private bool FilesSelected => _fileUpload is { Files.Count: > 0 };

    protected override void OnInitialized()
    {
        // Instead of awaiting this async enumerable here, let's capture it in a task
        // and start it in the background. This way, we can await it in the UI.
        category = new List<string>();
        DeleteService.OnChange += StateHasChanged;
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

        if (_fileUpload is { Files.Count: > 0 })
        {

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
                await _fileUpload.ResetAsync();
            }
        }
        
    }

    private ValueTask OnShowDocumentAsync(DocumentResponse document) =>
        PdfViewer.ShowDocumentAsync(document.Name, document.Url.ToString());

    private async ValueTask OnDeleteAsync(NavigationManager manager, string fileName)
    {
        
        //await JSRuntime.InvokeVoidAsync("addBeforeUnloadListener");
        var index = fileName.LastIndexOf("-");
        fileName = fileName.Substring(0, index) + ".pdf";
        
        await DeleteDocumentsEvent.InvokeAsync(fileName);
        //await JSRuntime.InvokeVoidAsync("removeBeforeUnloadListener");
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

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        DeleteService.OnChange -= StateHasChanged;

    }

}
