using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AutocadLlmPlugin.Application.Commands;

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
        var objectId = modelSpace.AppendNewly(circle);
        var payload = JsonSerializer.Serialize(new
        {
            id = ToIdString(objectId),
            type = "circle",
            center = new { x = centerX, y = centerY },
            radius
        });

        return CommandExecutionResult.CreateSuccess(
            $"Круг радиусом {radius.ToString("G", CultureInfo.InvariantCulture)} построен.",
            payload);
    });

    public CommandExecutionResult DrawLine(
        double startX,
        double startY,
        double endX,
        double endY) => ExecuteSafe((modelSpace, _) =>
    {
        var line = new Line(new Point3d(startX, startY, 0), new Point3d(endX, endY, 0));
        var objectId = modelSpace.AppendNewly(line);
        var payload = JsonSerializer.Serialize(new
        {
            id = ToIdString(objectId),
            type = "line",
            start = new { x = startX, y = startY },
            end = new { x = endX, y = endY }
        });

        return CommandExecutionResult.CreateSuccess("Отрезок построен.", payload);
    });

    public CommandExecutionResult DrawPolyline(
        IReadOnlyList<PolylineVertex> vertices,
        bool closed) => ExecuteSafe((modelSpace, _) =>
    {
        if (vertices.Count < 2)
            return CommandExecutionResult.CreateFailure("Нужно указать минимум две вершины полилинии.");

        var polyline = new Polyline();
        polyline.SetDatabaseDefaults();

        for (var i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            polyline.AddVertexAt(i, new Point2d(vertex.X, vertex.Y), vertex.Bulge, 0, 0);
        }

        polyline.Closed = closed;
        var objectId = modelSpace.AppendNewly(polyline);
        var payload = JsonSerializer.Serialize(new
        {
            id = ToIdString(objectId),
            type = "polyline",
            closed,
            vertices = vertices.Select((v, index) => new
            {
                index,
                x = v.X,
                y = v.Y,
                bulge = v.Bulge
            })
        });

        return CommandExecutionResult.CreateSuccess(
            $"Полилиния построена. Вершин: {vertices.Count}.",
            payload);
    });

    public CommandExecutionResult DrawObjects(IReadOnlyList<DrawingObjectRequest> objects)
    {
        if (objects.Count == 0)
            return CommandExecutionResult.CreateFailure("Список объектов для построения пуст.");

        var createdObjects = new List<object>();
        foreach (var request in objects)
        {
            var result = request.Kind switch
            {
                DrawingObjectKind.Circle => DrawCircle(request.Circle!.CenterX, request.Circle.CenterY, request.Circle.Radius),
                DrawingObjectKind.Line => DrawLine(request.Line!.StartX, request.Line.StartY, request.Line.EndX, request.Line.EndY),
                DrawingObjectKind.Polyline => DrawPolyline(request.Polyline!.Vertices, request.Polyline.Closed),
                _ => CommandExecutionResult.CreateFailure($"Неизвестный тип объекта: {request.Kind}.")
            };

            if (!result.IsSuccess)
                return result;

            var data = result.Data;
            if (!string.IsNullOrWhiteSpace(data))
                createdObjects.Add(JsonSerializer.Deserialize<JsonElement>(data!));
        }

        var payload = JsonSerializer.Serialize(new
        {
            created = createdObjects
        });

        return CommandExecutionResult.CreateSuccess("Объекты построены.", payload);
    }

    public CommandExecutionResult GetModelObjects() => ExecuteSafe((modelSpace, _) =>
    {
        var objects = new List<object>();

        foreach (var objectId in modelSpace)
        {
            if (objectId.GetObject(OpenMode.ForRead) is not Entity entity)
                continue;

            switch (entity)
            {
                case Circle circle:
                    objects.Add(new
                    {
                        id = ToIdString(objectId),
                        type = "circle",
                        center = new { x = circle.Center.X, y = circle.Center.Y },
                        radius = circle.Radius
                    });
                    break;
                case Line line:
                    objects.Add(new
                    {
                        id = ToIdString(objectId),
                        type = "line",
                        start = new { x = line.StartPoint.X, y = line.StartPoint.Y },
                        end = new { x = line.EndPoint.X, y = line.EndPoint.Y }
                    });
                    break;
                case Polyline polyline:
                    objects.Add(new
                    {
                        id = ToIdString(objectId),
                        type = "polyline",
                        closed = polyline.Closed,
                        vertices = Enumerable.Range(0, polyline.NumberOfVertices).Select(i => new
                        {
                            index = i,
                            x = polyline.GetPoint2dAt(i).X,
                            y = polyline.GetPoint2dAt(i).Y,
                            bulge = polyline.GetBulgeAt(i)
                        })
                    });
                    break;
            }
        }

        var payload = JsonSerializer.Serialize(new { objects });
        var message = objects.Count == 0
            ? "В модели нет поддерживаемых объектов."
            : $"Найдено объектов: {objects.Count}.";

        return CommandExecutionResult.CreateSuccess(message, payload);
    }, requiresWrite: false);

    public CommandExecutionResult DeleteObjects(IReadOnlyList<string> objectIds) => ExecuteSafe((modelSpace, _) =>
    {
        if (objectIds.Count == 0)
            return CommandExecutionResult.CreateFailure("Не переданы идентификаторы для удаления.");

        var deleted = new List<string>();
        var notFound = new List<string>();

        foreach (var idStr in objectIds)
        {
            if (!modelSpace.Database.TryGetObjectId(idStr, out var objectId, out var _))
            {
                notFound.Add(idStr);
                continue;
            }

            try
            {
                if (!objectId.IsValid)
                {
                    notFound.Add(idStr);
                    continue;
                }

                var dbObject = objectId.GetObject(OpenMode.ForWrite, false, true);
                if (dbObject is not Entity entity || entity.IsErased)
                {
                    notFound.Add(idStr);
                    continue;
                }

                entity.Erase();
                deleted.Add(idStr);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                notFound.Add(idStr);
            }
        }

        var payload = JsonSerializer.Serialize(new
        {
            deleted,
            notFound
        });

        var message = deleted.Count == 0
            ? "Не удалось удалить объекты."
            : $"Удалено объектов: {deleted.Count}.";

        if (notFound.Count > 0)
            message += $" Не найдены: {string.Join(", ", notFound)}.";

        return CommandExecutionResult.CreateSuccess(message.Trim(), payload);
    });

    public CommandExecutionResult ExecuteLisp(string code) => ExecuteSafe((_, _) =>
    {
        if (string.IsNullOrWhiteSpace(code))
            return CommandExecutionResult.CreateFailure("Передан пустой LISP-код.");

        try
        {
            dynamic acadCom = AcadApplication.AcadApplication;
            if (acadCom is null)
                return CommandExecutionResult.CreateFailure("COM-интерфейс AutoCAD недоступен.");

            object rawResult;
            try
            {
                rawResult = acadCom.Eval(code);
            }
            catch (Exception ex)
            {
                return CommandExecutionResult.CreateFailure($"Ошибка выполнения LISP: {ex.Message}");
            }

            if (rawResult is null)
                return CommandExecutionResult.CreateSuccess("LISP выполнен. Результат отсутствует.");

            var payload = JsonSerializer.Serialize(
                new { result = ConvertComLispResult(rawResult) },
                new JsonSerializerOptions { WriteIndented = true });

            return CommandExecutionResult.CreateSuccess("LISP выполнен.", payload);
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.CreateFailure($"Не удалось выполнить LISP: {ex.Message}");
        }
    });

    public CommandExecutionResult GetPolylineVertices() => ExecuteSafe((_, doc) =>
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

        var payload = JsonSerializer.Serialize(new
        {
            id = ToIdString(res.ObjectId),
            closed = polyline.Closed,
            vertices
        }, new JsonSerializerOptions { WriteIndented = true });

        return CommandExecutionResult.CreateSuccess("Данные полилинии получены.", payload);
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

    private static string ToIdString(ObjectId objectId) => objectId.Handle.Value.ToString(CultureInfo.InvariantCulture);

    private static object? ConvertComLispResult(object? value) =>
        value switch
        {
            null => null,
            double or float or int or long or short or bool or string => value,
            Array array => array.Cast<object?>().Select(ConvertComLispResult).ToList(),
            Point2d point2d => new { x = point2d.X, y = point2d.Y },
            Point3d point3d => new { x = point3d.X, y = point3d.Y, z = point3d.Z },
            Vector2d vector2d => new { x = vector2d.X, y = vector2d.Y },
            Vector3d vector3d => new { x = vector3d.X, y = vector3d.Y, z = vector3d.Z },
            ObjectId objectId => ToIdString(objectId),
            Handle handle => handle.ToString(),
            _ => value.ToString()
        };
}
