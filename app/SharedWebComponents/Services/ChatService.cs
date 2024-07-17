// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedWebComponents.Services;
public class ChatService
{
    private List<ChatInstance> _chatInstances = new List<ChatInstance>();

    public event Action OnChange;
    public event Action<Guid> ChatUpdated; // Event to notify when a chat is updated

    public void UpdateChatHistory(Guid chatId, UserQuestion _currentQuestion, ChatAppResponseOrError? response)
    {
        // remove the most recent chatappresponse in this chathistory

        var chatInstance = GetChatInstanceById(chatId);
        var mostRecentEntry = chatInstance.ChatHistory
          .OrderByDescending(entry => entry.Key.AskedOn)
          .FirstOrDefault();
        chatInstance.ChatHistory.Remove(mostRecentEntry.Key);

        if (chatInstance != null )
        {
            chatInstance.ChatHistory[_currentQuestion] = response;
            ChatUpdated?.Invoke(chatId); // Raise the event
        }
    }

    public void AddChatInstance(ChatInstance chatInstance)
    {
        _chatInstances.Add(chatInstance);
        NotifyStateChanged();
    }

    public IEnumerable<ChatInstance> GetChatInstances() => _chatInstances;

    public ChatInstance GetChatInstanceById(Guid id) => _chatInstances.FirstOrDefault(c => c.Id == id);

    private void NotifyStateChanged() => OnChange?.Invoke();
}
