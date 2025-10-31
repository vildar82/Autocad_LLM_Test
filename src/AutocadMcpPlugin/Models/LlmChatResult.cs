namespace AutocadMcpPlugin;

/// <summary>
/// Результат запроса к LLM.
/// </summary>
public sealed class LlmChatResult
{
    private LlmChatResult(bool isSuccess, string? content, string? error)
    {
        IsSuccess = isSuccess;
        Content = content;
        Error = error;
    }

    public bool IsSuccess { get; }

    public string? Content { get; }

    public string? Error { get; }

    public static LlmChatResult Success(string content) => new(true, content, null);

    public static LlmChatResult Failure(string error) => new(false, null, error);
}
