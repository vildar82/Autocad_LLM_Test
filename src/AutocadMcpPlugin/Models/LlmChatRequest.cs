using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AutocadMcpPlugin;

/// <summary>
/// Запрос к LLM на генерацию ответа.
/// </summary>
public sealed class LlmChatRequest
{
    public LlmChatRequest(IEnumerable<LlmMessage> messages, string? model = null, double? temperature = null)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        Messages = new ReadOnlyCollection<LlmMessage>(new List<LlmMessage>(messages));
        Model = model;
        Temperature = temperature;
    }

    public IReadOnlyList<LlmMessage> Messages { get; }

    public string? Model { get; }

    public double? Temperature { get; }
}