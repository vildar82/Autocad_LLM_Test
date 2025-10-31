using System.Collections.Generic;

namespace AutocadMcpPlugin;

/// <summary>
/// Выполняет команды и запросы к чертежу AutoCAD.
/// </summary>
public interface IAutocadCommandExecutor
{
    CommandExecutionResult DrawCircle(
        double centerX,
        double centerY,
        double radius);

    CommandExecutionResult DrawLine(
        double startX,
        double startY,
        double endX,
        double endY);

    CommandExecutionResult DrawPolyline(
        IReadOnlyList<PolylineVertex> vertices,
        bool closed);

    CommandExecutionResult DrawObjects(
        IReadOnlyList<DrawingObjectRequest> objects);

    CommandExecutionResult GetModelObjects();

    CommandExecutionResult DeleteObjects(
        IReadOnlyList<string> objectIds);

    CommandExecutionResult GetPolylineVertices();
}
