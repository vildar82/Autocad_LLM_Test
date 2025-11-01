using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using static AutocadLlmPlugin.CommandExecutionResult;
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

        return CreateSuccess(
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

        return CreateSuccess("Отрезок построен.", payload);
    });

    public CommandExecutionResult DrawPolyline(
        IReadOnlyList<PolylineVertex> vertices,
        bool closed) => ExecuteSafe((modelSpace, _) =>
    {
        if (vertices.Count < 2)
            return CreateFailure("Нужно указать минимум две вершины полилинии.");

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

        return CreateSuccess(
            $"Полилиния построена. Вершин: {vertices.Count}.",
            payload);
    });

    public CommandExecutionResult DrawObjects(IReadOnlyList<DrawingObjectRequest> objects)
    {
        if (objects.Count == 0)
            return CreateFailure("Список объектов для построения пуст.");

        var createdObjects = new List<object>();
        foreach (var request in objects)
        {
            var result = request.Kind switch
            {
                DrawingObjectKind.Circle => DrawCircle(request.Circle!.CenterX, request.Circle.CenterY, request.Circle.Radius),
                DrawingObjectKind.Line => DrawLine(request.Line!.StartX, request.Line.StartY, request.Line.EndX, request.Line.EndY),
                DrawingObjectKind.Polyline => DrawPolyline(request.Polyline!.Vertices, request.Polyline.Closed),
                _ => CreateFailure($"Неизвестный тип объекта: {request.Kind}.")
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

        return CreateSuccess("Объекты построены.", payload);
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

        return CreateSuccess(message, payload);
    }, requiresWrite: false);

    public CommandExecutionResult DeleteObjects(IReadOnlyList<string> objectIds) => ExecuteSafe((modelSpace, _) =>
    {
        if (objectIds.Count == 0)
            return CreateFailure("Не переданы идентификаторы для удаления.");

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

        return CreateSuccess(message.Trim(), payload);
    });

    public CommandExecutionResult ExecuteLisp(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return CreateFailure("Передан пустой LISP-код.");

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
            return CreateFailure("Активный документ AutoCAD не найден.");

        var outputPath = PrepareLispOutputPath();
        var wrappedCode = BuildWrappedLisp(code, outputPath);
        document.SendStringToExecute(wrappedCode, false, false, true);

        return CreateSuccess(
            "LISP отправлен на выполнение. Чтобы получить результат, вызови инструмент read_lisp_output после завершения.\n" +
            "Для вывода данных используйте (princ) или верните значение последним выражением.");
    }

    public CommandExecutionResult ReadLispOutput()
    {
        var outputPath = GetLispOutputPath();

        if (!File.Exists(outputPath))
            return CreateFailure("Файл lisp-output.txt ещё не создан. Повтори запрос позже.");

        try
        {
            var text = File.ReadAllText(outputPath, Encoding.Default).Trim();
            File.Delete(outputPath);

            if (string.IsNullOrEmpty(text))
                return CreateSuccess("Файл результата пуст.");

            if (text.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                return CreateFailure(text);

            var payload = JsonSerializer.Serialize(
                new { output = text },
                new JsonSerializerOptions { WriteIndented = true });

            return CreateSuccess("Результат LISP получен.", payload);
        }
        catch (IOException ex)
        {
            return CreateFailure($"Не удалось прочитать результат LISP: {ex.Message}");
        }
    }

    public CommandExecutionResult GetPolylineVertices() => ExecuteSafe((_, doc) =>
    {
        var opt = new PromptEntityOptions("\nВыбери полилинию");
        opt.SetRejectMessage("\nНужно выбрать полилинию");
        opt.AddAllowedClass(typeof(Polyline), true);
        var res = doc.Editor.GetEntity(opt);
        if (res.Status != PromptStatus.OK)
            return CreateFailure("Полилиния не выбрана");

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

        return CreateSuccess("Данные полилинии получены.", payload);
    }, requiresWrite: false);

    private static CommandExecutionResult ExecuteSafe(
        Func<BlockTableRecord, Document, CommandExecutionResult> action,
        bool requiresWrite = true)
    {
        try
        {
            var document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
                return CreateFailure("Активный документ AutoCAD не найден.");

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
            return CreateFailure($"Ошибка AutoCAD: {autocadException.Message}");
        }
        catch (System.Exception ex)
        {
            return CreateFailure($"Не удалось выполнить команду: {ex.Message}");
        }
    }

    private static string ToIdString(ObjectId objectId) => objectId.Handle.Value.ToString(CultureInfo.InvariantCulture);

    private static string PrepareLispOutputPath()
    {
        var path = GetLispOutputPath();
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // файл мог отсутствовать или быть заблокирован предыдущим запуском
        }

        return path;
    }

    private static string GetLispOutputPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutocadLlmPlugin");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "lisp-output.txt");
    }

    private static string BuildWrappedLisp(string userCode, string outputPath)
    {
        var escapedPath = outputPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var builder = new StringBuilder();
        builder.AppendLine("(progn");
        builder.AppendLine("  (vl-load-com)");
        builder.AppendLine("  (defun _llm-write (value)");
        builder.AppendLine($"    (setq __file (open \"{escapedPath}\" \"w\"))");
        builder.AppendLine("    (if __file");
        builder.AppendLine("      (progn");
        builder.AppendLine("        (write-line (vl-princ-to-string value) __file)");
        builder.AppendLine("        (close __file))))");
        builder.AppendLine("  (setq __llm-result");
        builder.AppendLine("        (vl-catch-all-apply");
        builder.AppendLine("          (function (lambda ()");
        builder.Append("            ");
        builder.AppendLine(userCode);
        builder.AppendLine("          ))))");
        builder.AppendLine("  (if (vl-catch-all-error-p __llm-result)");
        builder.AppendLine("      (_llm-write (strcat \"ERROR: \" (vl-catch-all-error-message __llm-result)))");
        builder.AppendLine("      (_llm-write __llm-result))");
        builder.AppendLine("  (princ))");
        return builder.ToString();
    }

    private static void SendStringToExecute(Document document, string code)
    {
        var command = code.EndsWith("\n", StringComparison.Ordinal)
            ? code
            : code + "\n";

        document.SendStringToExecute(command, false, false, true);
    }
}