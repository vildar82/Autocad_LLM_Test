using System.Threading;
using System.Threading.Tasks;

namespace AutocadMcpPlugin.Application.Commands;

/// <summary>
/// Выполняет команды построения примитивов в AutoCAD.
/// </summary>
public interface IAutocadCommandExecutor
{
    Task<CommandExecutionResult> DrawCircleAsync(
        double centerX,
        double centerY,
        double radius,
        CancellationToken cancellationToken = default);

    Task<CommandExecutionResult> DrawLineAsync(
        double startX,
        double startY,
        double endX,
        double endY,
        CancellationToken cancellationToken = default);
}
