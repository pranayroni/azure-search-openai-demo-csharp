﻿// Copyright (c) Microsoft. All rights reserved.

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

    protected override void OnInitialized()
    {
        _currentChat = ChatService.GetChatInstanceById(ChatId);
        _questionAndAnswerMap = _currentChat.ChatHistory;
    }
    protected override async Task OnParametersSetAsync()
    {
        _currentChat = ChatService.GetChatInstanceById(ChatId);
        _questionAndAnswerMap = _currentChat.ChatHistory;
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

        _isReceivingResponse = true;
        _lastReferenceQuestion = _userQuestion;
        _currentQuestion = new(_userQuestion, DateTime.Now);
        _questionAndAnswerMap[_currentQuestion] = null;

        try
        {
            var history = _questionAndAnswerMap
                .Where(x => x.Value?.Choices is { Length: > 0})
                .SelectMany(x => new ChatMessage[] { new ChatMessage("user", x.Key.Question), new ChatMessage("assistant", x.Value!.Choices[0].Message.Content) })
                .ToList();

            history.Add(new ChatMessage("user", _userQuestion));

            Settings.Overrides.ExcludeCategory = Settings2.Overrides.ExcludeCategory.ToList<string>() ?? new List<string>();

            var request = new ChatRequest([.. history], Settings.Overrides);
            var result = await ApiClient.ChatConversationAsync(request);

            _questionAndAnswerMap[_currentQuestion] = result.Response;
            if (result.IsSuccessful)
            {
                _userQuestion = "";
                _currentQuestion = default;
            }
        }
        finally
        {
            _isReceivingResponse = false;
        }
    }

    private void OnClearChat()
    {
        _userQuestion = _lastReferenceQuestion = "";
        _currentQuestion = default;
        _questionAndAnswerMap.Clear();
    }
}
