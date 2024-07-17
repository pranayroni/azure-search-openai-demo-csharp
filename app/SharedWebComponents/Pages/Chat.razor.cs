// Copyright (c) Microsoft. All rights reserved.

namespace SharedWebComponents.Pages;

public sealed partial class Chat
{
    private string _userQuestion = "";
    private UserQuestion _currentQuestion;
    private string _lastReferenceQuestion = "";
    private bool _isReceivingResponse = false;

    private Dictionary<UserQuestion, ChatAppResponseOrError?> _questionAndAnswerMap = [];

    [Inject] public required ISessionStorageService SessionStorage { get; set; }

    [Inject] public required ApiClient ApiClient { get; set; }

    [CascadingParameter(Name = nameof(Settings))]
    public required RequestSettingsOverrides Settings { get; set; }

    [CascadingParameter(Name = nameof(Settings2))]
    public required RequestSettingsOverrides Settings2 { get; set; }

    [CascadingParameter(Name = nameof(IsReversed))]
    public required bool IsReversed { get; set; }

    [Parameter]
    public Guid ChatId { get; set; }

    private ChatInstance _currentChat;

    private void UpdateChatHistory()
    {
        _currentChat = ChatService.GetChatInstanceById(ChatId);
        if (_currentChat != null)
        {
            _questionAndAnswerMap = _currentChat.ChatHistory;
        }
    }

    protected override void OnInitialized()
    {
        UpdateChatHistory();
        ChatService.ChatUpdated += HandleChatUpdated;
    }
    private void HandleChatUpdated(Guid chatId)
    {
        if (chatId == this.ChatId)
        {
            // the current chat was updated; refresh the component
            InvokeAsync(() =>
            {
                UpdateChatHistory();
                StateHasChanged();
            });
        }
    }
    public void Dispose() // thread safety !! 
    {
        ChatService.ChatUpdated -= HandleChatUpdated;
    }
    protected override async Task OnParametersSetAsync()
    {
        _currentChat = ChatService.GetChatInstanceById(ChatId);
        if(_currentChat!=null)
        {
            _questionAndAnswerMap = _currentChat.ChatHistory;
        }
        await base.OnParametersSetAsync();
    }

    private Task OnAskQuestionAsync(string question)
    {
        _userQuestion = question;
        return OnAskClickedAsync();
    }

    private async Task OnEnterKeyPressedAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await OnAskClickedAsync();
        }
    }

    private async Task OnAskClickedAsync()
    {
        if (string.IsNullOrWhiteSpace(_userQuestion))
        {
            return;
        }

        var requestChatId = this.ChatId; // Capture the current chat ID at the start of the request because this is an async op
        var requestQuestion = new UserQuestion(_userQuestion, DateTime.Now); // Capture the current question at the start of the request because this is an async op
        _isReceivingResponse = true;
        _lastReferenceQuestion = _userQuestion;
        _currentQuestion = new(_userQuestion, DateTime.Now);
        ChatService.GetChatInstanceById(requestChatId).ChatHistory[_currentQuestion] = null; // assign null to indicate that the response is being fetched

        try
        {
            var history = ChatService.GetChatInstanceById(requestChatId).ChatHistory
                .Where(x => x.Value?.Choices is { Length: > 0})
                .SelectMany(x => new ChatMessage[] { new ChatMessage("user", x.Key.Question), new ChatMessage("assistant", x.Value!.Choices[0].Message.Content) })
                .ToList();

            history.Add(new ChatMessage("user", _userQuestion));

            Settings.Overrides.ExcludeCategory = Settings2.Overrides.ExcludeCategory.ToList<string>() ?? new List<string>();

            var request = new ChatRequest([.. history], Settings.Overrides);
            var result = await ApiClient.ChatConversationAsync(request);

            // _questionAndAnswerMap[_currentQuestion] = result.Response;
            ChatService.UpdateChatHistory(requestChatId, requestQuestion, result.Response);
          
        }
        finally
        {
            _isReceivingResponse = false;
                _userQuestion = "";
                _currentQuestion = default;
        }
    }

    private void OnClearChat()
    {
        _userQuestion = _lastReferenceQuestion = "";
        _currentQuestion = default;
        _questionAndAnswerMap.Clear();
        ChatService.GetChatInstanceById(this.ChatId).ChatHistory.Clear();
    }
}
