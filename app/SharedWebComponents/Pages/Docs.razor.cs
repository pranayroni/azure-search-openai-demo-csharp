// Copyright (c) Microsoft. All rights reserved.

using Newtonsoft.Json.Linq;

namespace SharedWebComponents.Pages;

public sealed partial class Docs : IDisposable
{
    private const long MaxIndividualFileSize = 1_024L * 1_024;

    private MudForm _form = null!;
    private MudFileUpload<IReadOnlyList<IBrowserFile>> _fileUpload = null!;
    private Task _getDocumentsTask = null!;
    private Task _getCategoriesTask = null!;
    private bool _isLoadingDocuments = false;
    private bool _isUploadingDocuments = false;
    private string _filter = "";

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<DocumentResponse> _documents = [];

    public IEnumerable<string> category = new List<string>();
    public List<string>? cList = null;
    public string jsonResponse = string.Empty;


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
        _getDocumentsTask = GetDocumentsAsync();
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
                _fileUpload.Files, MaxIndividualFileSize, cookie, string.Join(',', category));

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

    protected override async Task OnInitializedAsync()
    {
        category = new List<string>();
        var httpClient = new HttpClient();
        //httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //httpClient.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpRequestMessage request = new();
        request.RequestUri = new Uri("https://gptkb-r6lomx22dqabk.search.windows.net/indexes/gptkbindex2/docs?api-version=2024-05-01-preview&facet=category,count:1000");
        request.Method = HttpMethod.Get;
        request.Headers.Add("api-key", "ARNSvbnPWMRETL0rcPw3VmB0T1Fhsa4fnCFSkTBSKwAzSeAMgWiZ");
        //request.Headers.Add("Access-Control-Allow-Origin", "*");

        //request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var endpoint = new Uri("https://gptkb-r6lomx22dqabk.search.windows.net/indexes/gptkbindex2/docs?api-version=2024-05-01-preview&facet=category,count:1000");

        try
        {

            // Assuming GetCategoriesAsync returns a List<string> or similar collection of category names.

            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                //jsonResponse = await response.Content.ReadAsStringAsync();
                // Assuming the API returns a JSON array of strings.
                //cList = JsonConvert.DeserializeObject<List<string>>(jsonResponse);
                jsonResponse = await response.Content.ReadAsStringAsync();
                var categories = JObject.Parse(jsonResponse)["@search.facets"]["category"]
                    .Select(c => c["value"].ToString())
                    .ToList();
                cList = categories;
            }
            else
            {
                // Handle error or throw an exception
                throw new HttpRequestException($"Failed to fetch categories. Status code: {response.StatusCode} Message: {response.Content}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching categories: {ex.Message}");
            throw new Exception($"Error fetching categories: {ex.Message}");
        }

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
