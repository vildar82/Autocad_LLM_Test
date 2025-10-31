using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AutocadMcpPlugin;

/// <summary>
/// Сообщение в истории диалога LLM.
/// </summary>
public sealed class LlmMessage
{
    private LlmMessage(string role, string content, string? toolCallId, IReadOnlyList<LlmToolCall>? toolCalls)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content;
        ToolCallId = toolCallId;
        ToolCalls = toolCalls ?? [];
    }

    public string Role { get; }

    public string Content { get; }

    public string? ToolCallId { get; }

    public IReadOnlyList<LlmToolCall> ToolCalls { get; }

    public static LlmMessage CreateSystem(string content) =>
        new("system", content, null, null);

    public static LlmMessage CreateUser(string content) =>
        new("user", content, null, null);

    public static LlmMessage CreateAssistant(string content) =>
        new("assistant", content, null, null);

    public static LlmMessage CreateAssistantWithToolCalls(IEnumerable<LlmToolCall> toolCalls, string? content = null)
    {
        var calls = new ReadOnlyCollection<LlmToolCall>(new List<LlmToolCall>(toolCalls));
        return new LlmMessage("assistant", content ?? string.Empty, null, calls);
    }

    public static LlmMessage CreateTool(string toolCallId, string content) =>
        new("tool", content, toolCallId, null);
}