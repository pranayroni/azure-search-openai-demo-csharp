// Copyright (c) Microsoft. All rights reserved.

namespace SharedWebComponents.Models;

public class ChatInstance
{
    public string Name { get; set; } = "New Chat #"; // Name of the chat
    public Guid Id { get; set; } = Guid.NewGuid(); // Unique identifier for each chat
    public Dictionary<UserQuestion, ChatAppResponseOrError?> ChatHistory { get; set; } = new Dictionary<UserQuestion, ChatAppResponseOrError?>();
}
