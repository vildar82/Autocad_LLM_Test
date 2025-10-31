namespace AutocadLlmPlugin;

/// <summary>
/// Результат выполнения команды AutoCAD.
/// </summary>
public sealed class CommandExecutionResult
{
    private CommandExecutionResult(bool isSuccess, string message, string? data)
    {
        IsSuccess = isSuccess;
        Message = message;
        Data = data;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    public string? Data { get; }

    public static CommandExecutionResult CreateSuccess(string message, string? data = null) =>
        new(true, message, data);

    public static CommandExecutionResult CreateFailure(string message) =>
        new(false, message, null);
}
