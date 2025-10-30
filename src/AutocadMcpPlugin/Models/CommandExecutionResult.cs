
namespace AutocadMcpPlugin;

/// <summary>
/// Результат выполнения команды AutoCAD.
/// </summary>
public sealed class CommandExecutionResult(bool isSuccess, string message)
{
    public bool IsSuccess { get; } = isSuccess;

    public string Message { get; } = message;

    public static CommandExecutionResult CreateSuccess(string message) =>
        new(true, message);

    public static CommandExecutionResult CreateFailure(string message) =>
        new(false, message);
}