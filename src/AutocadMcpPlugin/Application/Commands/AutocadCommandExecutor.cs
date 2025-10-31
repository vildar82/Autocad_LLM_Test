using System;
using System.Threading;
using System.Threading.Tasks;
using AutocadMcpPlugin;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AutocadMcpPlugin.Application.Commands;

/// <summary>
/// Реализация команд построения примитивов в AutoCAD.
/// </summary>
public sealed class AutocadCommandExecutor : IAutocadCommandExecutor
{
    public Task<CommandExecutionResult> DrawCircleAsync(
        double centerX,
        double centerY,
        double radius,
        CancellationToken cancellationToken = default) => ExecuteSafe(modelSpace =>
    {
        var center = new Point3d(centerX, centerY, 0);
        var circle = new Circle(center, Vector3d.ZAxis, radius);
        modelSpace.AppendNewly(circle);
        return CommandExecutionResult.CreateSuccess($"Круг радиусом {radius} создан.");
    }, cancellationToken);

    public Task<CommandExecutionResult> DrawLineAsync(
        double startX,
        double startY,
        double endX,
        double endY,
        CancellationToken cancellationToken = default) => ExecuteSafe(modelSpace =>
    {
        var line = new Line(new Point3d(startX, startY, 0), new Point3d(endX, endY, 0));
        modelSpace.AppendNewly(line);
        return CommandExecutionResult.CreateSuccess("Линия создана.");
    }, cancellationToken);

    private static Task<CommandExecutionResult> ExecuteSafe(
        Func<BlockTableRecord, CommandExecutionResult> action,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<CommandExecutionResult>(cancellationToken);

        try
        {
            var document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
                return Task.FromResult(CommandExecutionResult.CreateFailure("Активный документ AutoCAD не найден."));

            using var lockDocument = document.LockDocument();
            var database = document.Database;
            using var transaction = database.TransactionManager.StartTransaction();
            var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var result = action(modelSpace);
            transaction.Commit();
            return Task.FromResult(result);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception autocadException)
        {
            return Task.FromResult(CommandExecutionResult.CreateFailure($"Ошибка AutoCAD: {autocadException.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandExecutionResult.CreateFailure($"Не удалось выполнить команду: {ex.Message}"));
        }
    }
}
