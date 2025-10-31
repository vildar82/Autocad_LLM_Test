using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
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
        var objectId = modelSpace.AppendNewly(circle);
        var payload = JsonSerializer.Serialize(new
        {
            id = ToIdString(objectId),
            handle = circle.Handle.ToString(),
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
            handle = line.Handle.ToString(),
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
        if (vertices is null || vertices.Count < 2)
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
            handle = polyline.Handle.ToString(),
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
        if (objects is null || objects.Count == 0)
            return CommandExecutionResult.CreateFailure("Список объектов для построения пуст.");

        var createdObjects = new List<object>();
        foreach (var request in objects)
        {
            CommandExecutionResult result = request.Kind switch
            {
                DrawingObjectKind.Circle => DrawCircle(request.Circle!.CenterX, request.Circle.CenterY, request.Circle.Radius),
                DrawingObjectKind.Line => DrawLine(request.Line!.StartX, request.Line.StartY, request.Line.EndX, request.Line.EndY),
                DrawingObjectKind.Polyline => DrawPolyline(request.Polyline!.Vertices, request.Polyline.Closed),
                _ => CommandExecutionResult.CreateFailure($"Неизвестный тип объекта: {request.Kind}.")
            };

            if (!result.IsSuccess)
                return result;

            if (!string.IsNullOrWhiteSpace(result.Data))
                createdObjects.Add(JsonSerializer.Deserialize<JsonElement>(result.Data));
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

        foreach (ObjectId objectId in modelSpace)
        {
            if (objectId.GetObject(OpenMode.ForRead) is not Entity entity)
                continue;

            switch (entity)
            {
                case Circle circle:
                    objects.Add(new
                    {
                        id = ToIdString(objectId),
                        handle = circle.Handle.ToString(),
                        type = "circle",
                        center = new { x = circle.Center.X, y = circle.Center.Y },
                        radius = circle.Radius
                    });
                    break;
                case Line line:
                    objects.Add(new
                    {
                        id = ToIdString(objectId),
                        handle = line.Handle.ToString(),
                        type = "line",
                        start = new { x = line.StartPoint.X, y = line.StartPoint.Y },
                        end = new { x = line.EndPoint.X, y = line.EndPoint.Y }
                    });
                    break;
                case Polyline polyline:
                    objects.Add(new
                    {
                        id = ToIdString(objectId),
                        handle = polyline.Handle.ToString(),
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
        if (objectIds is null || objectIds.Count == 0)
            return CommandExecutionResult.CreateFailure("Не переданы идентификаторы для удаления.");

        var deleted = new List<string>();
        var notFound = new List<string>();

        foreach (var id in objectIds)
        {
            if (!TryGetObjectId(modelSpace.Database, id, out var objectId, out var error))
            {
                notFound.Add(id);
                continue;
            }

            try
            {
                if (!objectId.IsValid)
                {
                    notFound.Add(id);
                    continue;
                }

                var dbObject = objectId.GetObject(OpenMode.ForWrite, false, true);
                if (dbObject is not Entity entity)
                {
                    notFound.Add(id);
                    continue;
                }

                if (entity.IsErased)
                {
                    notFound.Add(id);
                    continue;
                }

                entity.Erase();
                deleted.Add(id);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                notFound.Add(id);
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

        var payload = JsonSerializer.Serialize(new
        {
            id = ToIdString(res.ObjectId),
            handle = polyline.Handle.ToString(),
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
        catch (System.Exception ex)
        {
            return CommandExecutionResult.CreateFailure($"Не удалось выполнить команду: {ex.Message}");
        }
    }

    private static string ToIdString(ObjectId objectId) => objectId.Handle.Value.ToString(CultureInfo.InvariantCulture);

    private static bool TryGetObjectId(Database database, string id, out ObjectId objectId, out string error)
    {
        objectId = ObjectId.Null;
        error = string.Empty;

        if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            error = $"Некорректный идентификатор объекта: {id}.";
            return false;
        }

        try
        {
            var handle = new Handle(value);
            objectId = database.GetObjectId(false, handle, 0);
            if (objectId == ObjectId.Null)
            {
                error = $"Объект с Id {id} не найден.";
                return false;
            }

            return true;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            error = $"Объект с Id {id} не найден.";
            return false;
        }
    }
}
