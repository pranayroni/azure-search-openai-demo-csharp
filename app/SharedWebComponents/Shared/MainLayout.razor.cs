// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using System.Reflection.Metadata;
using System.Threading;
using Azure.Storage.Blobs;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using static MudBlazor.Colors;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static MudBlazor.CategoryTypes;
using SharedWebComponents.Pages;
using System.Reflection;



namespace SharedWebComponents.Shared;

public sealed partial class MainLayout
{
    private readonly MudTheme _theme = new();
    private bool _drawerOpen = false;
    private bool _settingsOpen = false;
    private SettingsPanel? _settingsPanel;
    public required ILogger<MainLayout> Logger { get; set; }

    private bool _isDarkTheme
    {
        get => LocalStorage.GetItem<bool>(StorageKeys.PrefersDarkTheme);
        set => LocalStorage.SetItem<bool>(StorageKeys.PrefersDarkTheme, value);
    }

    private bool _isReversed
    {
        get => LocalStorage.GetItem<bool?>(StorageKeys.PrefersReversedConversationSorting) ?? false;
        set => LocalStorage.SetItem<bool>(StorageKeys.PrefersReversedConversationSorting, value);
    }

    private bool _isRightToLeft =>
        Thread.CurrentThread.CurrentUICulture is { TextInfo.IsRightToLeft: true };

    [Inject] public required NavigationManager Nav { get; set; }
    [Inject] public required ILocalStorageService LocalStorage { get; set; }
    [Inject] public required IDialogService Dialog { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required DeleteService DeleteService { get; set; }

    public RequestSettingsOverrides Settings2 { get; set; } = new();


    public List<string>? cList = null;


    [Inject]
    public required ApiClient Client { get; set; }

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<DocumentResponse> _documents = [];
    private readonly HashSet<string> _categories = [];
    private Task _getCategoriesTask = null!;
    private bool _isLoadingCategories = false;
    private bool _isUploadingDocuments = false;

    private const long MaxIndividualFileSize = 1_024L * 1_024;


    public EventCallback<UploadDocumentsArgs> UploadDocumentsEvent =>
        EventCallback.Factory.Create<UploadDocumentsArgs>(this, UploadDocumentsAsync);
    public EventCallback<string> DeleteDocumentsEvent =>
        EventCallback.Factory.Create<string>(this, DeleteDocumentsAsync);


    private async Task UploadDocumentsAsync (UploadDocumentsArgs args)
    {
        _isUploadingDocuments = true;
        StateHasChanged();
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

        var cancellationToken = _cancellationTokenSource.Token;
        var result = await Client.UploadDocumentsAsync(
            args.Files, MaxIndividualFileSize, cookie,
            args.Category, cancellationToken);
        var fileCount = args.Files.Count;

        if (result.IsSuccessful)
        {
            args.Success = true;
            Snackbar.Add(
                    $"Uploaded {result.UploadedFiles.Length} documents.",
                    Severity.Success,
                    static options =>
                    {
                        options.ShowCloseIcon = true;
                        options.VisibleStateDuration = 10_000;
                    });
        }
        else
        {
            args.Success = false;
            Snackbar.Add(
                    result.Error,
                    Severity.Error,
                    static options =>
                    {
                        options.ShowCloseIcon = true;
                        options.VisibleStateDuration = 10_000;
                    });

        }
        _isUploadingDocuments = false;
        StateHasChanged();

    }

    private async Task DeleteDocumentsAsync (string fileName)
    {
        DeleteService.UpdateIsDeleting(true);
        DeleteService.UpdateDeleteProgress(10);
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
        DeleteService.UpdateDeleteProgress(50);
        await Client.RequestDeleteEmbeddingsAsync(deleteRequest, cancellationToken);
        DeleteService.UpdateDeleteProgress(100);
        await Task.Delay(300);
        DeleteService.UpdateIsDeleting(false);
    }


    private bool SettingsDisabled => new Uri(Nav.Uri).Segments.LastOrDefault().TrimEnd('/') switch
    {
        "ask" or "chat" => false,
        _ => true
    };

    private bool SortDisabled => new Uri(Nav.Uri).Segments.LastOrDefault().TrimEnd('/') switch
    {
        "voicechat" or "chat" => false,
        _ => true
    };

    private void OnMenuClicked() => _drawerOpen = !_drawerOpen;

    private void OnThemeChanged() => _isDarkTheme = !_isDarkTheme;

    private void OnIsReversedChanged() => _isReversed = !_isReversed;

    protected override void OnInitialized()
    {
        DeleteService.OnChange += StateHasChanged;
        // Instead of awaiting this async enumerable here, let's capture it in a task
        // and start it in the background. This way, we can await it in the UI.
        Settings2.Overrides.ExcludeCategory = new List<string>();
        _getCategoriesTask = GetCategoriesAsync();
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

    public async Task<IEnumerable<string>> MySearchFuncAsync(string search)
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
