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



namespace SharedWebComponents.Shared;

public sealed partial class MainLayout
{
    private readonly MudTheme _theme = new();
    private bool _drawerOpen = false;
    private bool _settingsOpen = false;
    private SettingsPanel? _settingsPanel;

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

}
