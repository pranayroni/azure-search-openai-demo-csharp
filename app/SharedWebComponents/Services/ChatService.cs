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

    public void AddChatInstance(ChatInstance chatInstance)
    {
        _chatInstances.Add(chatInstance);
        NotifyStateChanged();
    }

    public IEnumerable<ChatInstance> GetChatInstances() => _chatInstances;

    public ChatInstance GetChatInstanceById(Guid id) => _chatInstances.FirstOrDefault(c => c.Id == id);

    private void NotifyStateChanged() => OnChange?.Invoke();
}
