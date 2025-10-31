using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AutocadMcpPlugin;

/// <summary>
/// Результат запроса к LLM.
/// </summary>
public sealed class LlmChatResult
{
    private LlmChatResult(bool isSuccess, string? content, string? error, IReadOnlyList<LlmToolCall> toolCalls)
    {
        IsSuccess = isSuccess;
        Content = content;
        Error = error;
        ToolCalls = toolCalls;
    }

    public bool IsSuccess { get; }

    public string? Content { get; }

    public string? Error { get; }

    public IReadOnlyList<LlmToolCall> ToolCalls { get; }

    public static LlmChatResult Success(string content, IEnumerable<LlmToolCall>? toolCalls = null)
    {
        var calls = toolCalls is null
            ? new List<LlmToolCall>()
            : new List<LlmToolCall>(toolCalls);

        return new LlmChatResult(true, content, null, new ReadOnlyCollection<LlmToolCall>(calls));
    }

    public static LlmChatResult ToolCallsOnly(IEnumerable<LlmToolCall> toolCalls)
    {
        var calls = new List<LlmToolCall>(toolCalls);
        return new LlmChatResult(true, null, null, new ReadOnlyCollection<LlmToolCall>(calls));
    }

    public static LlmChatResult Failure(string error) => new(false, null, error, []);
}