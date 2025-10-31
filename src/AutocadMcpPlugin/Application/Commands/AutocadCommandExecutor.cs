using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AutocadMcpPlugin.Application.Commands;

/// <summary>
/// Исполняет команды и запросы, обращающиеся к AutoCAD.
/// </summary>
public sealed class AutocadCommandExecutor : IAutocadCommandExecutor
{
    public CommandExecutionResult DrawCircle(
        double centerX,
        double centerY,
        double radius) => ExecuteSafe((modelSpace, _) =>
    {
        var center = new Point3d(centerX, centerY, 0);
        var circle = new Circle(center, Vector3d.ZAxis, radius);
        modelSpace.AppendNewly(circle);
        return CommandExecutionResult.CreateSuccess(
            $"Круг радиусом {radius.ToString("G", CultureInfo.InvariantCulture)} построен.");
    });

    public CommandExecutionResult DrawLine(
        double startX,
        double startY,
        double endX,
        double endY) => ExecuteSafe((modelSpace, _) =>
    {
        var line = new Line(new Point3d(startX, startY, 0), new Point3d(endX, endY, 0));
        modelSpace.AppendNewly(line);
        return CommandExecutionResult.CreateSuccess("Отрезок построен.");
    });

    public CommandExecutionResult GetPolylineVertices() => ExecuteSafe((modelSpace, doc) =>
    {
        var opt = new PromptEntityOptions("\nВыбери полилинию");
        opt.SetRejectMessage("\nНужно выбрать полилинию");
        opt.AddAllowedClass(typeof(Polyline), true);
        var res = doc.Editor.GetEntity(opt);
        if (res.Status != PromptStatus.OK)
            return CommandExecutionResult.CreateFailure("Полилиния не выбрана");

        var polyline = (Polyline)res.ObjectId.GetObject(OpenMode.ForRead, false, true);
        var vertices = new List<object>(polyline.NumberOfVertices);

        for (var i = 0; i < polyline.NumberOfVertices; i++)
        {
            var point = polyline.GetPoint2dAt(i);
            var bulge = polyline.GetBulgeAt(i);
            vertices.Add(new
            {
                index = i,
                x = point.X,
                y = point.Y,
                bulge
            });
        }

        var payload = new
        {
            handle = polyline.Handle.ToString(),
            closed = polyline.Closed,
            vertices
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return CommandExecutionResult.CreateSuccess(json);
    }, requiresWrite: false);

    private static CommandExecutionResult ExecuteSafe(
        Func<BlockTableRecord, Document, CommandExecutionResult> action,
        bool requiresWrite = true)
    {
        try
        {
            var document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
                return CommandExecutionResult.CreateFailure("Активный документ AutoCAD не найден.");

            using var lockDocument = document.LockDocument();
            var database = document.Database;
            using var transaction = database.TransactionManager.StartTransaction();
            var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var mode = requiresWrite ? OpenMode.ForWrite : OpenMode.ForRead;
            var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], mode);
            var result = action(modelSpace, document);
            transaction.Commit();
            return result;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception autocadException)
        {
            return CommandExecutionResult.CreateFailure($"Ошибка AutoCAD: {autocadException.Message}");
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.CreateFailure($"Не удалось выполнить команду: {ex.Message}");
        }
    }

    private static bool TryGetObjectId(Database database, string handle, out ObjectId objectId, out string error)
    {
        objectId = ObjectId.Null;
        error = string.Empty;

        try
        {
            var value = Convert.ToInt64(handle, 16);
            var acadHandle = new Handle(value);
            objectId = database.GetObjectId(false, acadHandle, 0);
            if (objectId == ObjectId.Null)
            {
                error = $"Объект с хэндлом {handle} не найден.";
                return false;
            }

            return true;
        }
        catch (FormatException)
        {
            error = $"Некорректный хэндл полилинии: {handle}.";
            return false;
        }
        catch (OverflowException)
        {
            error = $"Некорректный хэндл полилинии: {handle}.";
            return false;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            error = $"Объект с хэндлом {handle} не найден.";
            return false;
        }
    }
}