using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutocadLlmPlugin.Infrastructure.Configuration;

namespace AutocadLlmPlugin.Application.Conversations;

/// <summary>
/// Координатор диалога между LLM и инструментами AutoCAD.
/// </summary>
public sealed class ConversationCoordinator(
    IAutocadCommandExecutor commandExecutor,
    ILlmClient llmClient,
    OpenAiSettings settings)
    : IConversationCoordinator
{
    private readonly List<LlmMessage> _history =
    [
        LlmMessage.CreateSystem(
            "Ты ассистент AutoCAD. Выполняй запросы пользователей с помощью инструментов, которые тебе доступны."),
        LlmMessage.CreateSystem(
            "Если входных данных недостаточно, уточни детали у пользователя перед запуском инструментов.")
    ];

    private readonly IReadOnlyList<LlmToolDefinition> _toolDefinitions =
    [
        DrawCircleToolDefinition(),
        DrawLineToolDefinition(),
        DrawPolylineToolDefinition(),
        DrawObjectsToolDefinition(),
        GetObjectsToolDefinition(),
        DeleteObjectsToolDefinition(),
        ExecuteLispToolDefinition(),
        ReadLispOutputToolDefinition(),
        GetPolylineToolDefinition()
    ];

    // Основной цикл: фиксируем запрос пользователя, передаем историю и описание инструментов в LLM,
    // а затем обрабатываем цепочки tool-call(ов), пока ассистент не сформирует текстовый ответ.
    public async Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Пустое сообщение.";

        _history.Add(LlmMessage.CreateUser(message));

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            const string noKey = "Не удалось обратиться к LLM. Укажи API-ключ OpenAI в настройках.";
            _history.Add(LlmMessage.CreateAssistant(noKey));
            return noKey;
        }

        for (var iteration = 0; iteration < 6; iteration++)
        {
            var request = new LlmChatRequest(TrimHistory(), _toolDefinitions);
            var response = await llmClient.CreateChatCompletionAsync(request, cancellationToken);

            if (!response.IsSuccess)
            {
                var errorMessage = response.Error ?? "Не удалось получить ответ от LLM.";
                _history.Add(LlmMessage.CreateAssistant(errorMessage));
                return errorMessage;
            }

            if (response.ToolCalls.Count > 0)
            {
                _history.Add(LlmMessage.CreateAssistantWithToolCalls(response.ToolCalls, response.Content));

                foreach (var toolCall in response.ToolCalls)
                {
                    var toolResult = ExecuteToolCall(toolCall);
                    _history.Add(LlmMessage.CreateTool(toolCall.Id, toolResult));
                }

                continue;
            }

            if (!string.IsNullOrEmpty(response.Content))
            {
                // LLM завершила диалог текстом и дополнительных действий не требуется.
                var finalContent = response.Content!;
                _history.Add(LlmMessage.CreateAssistant(finalContent));
                return finalContent;
            }
        }

        const string fallback = "Не удалось согласовать ответ с LLM.";
        _history.Add(LlmMessage.CreateAssistant(fallback));
        return fallback;
    }

    private string ExecuteToolCall(LlmToolCall toolCall)
    {
        try
        {
            // Маршрутизируем вызов в зависимости от имени инструмента.
            return toolCall.Name switch
            {
                "draw_circle" => ExecuteDrawCircle(toolCall),
                "draw_line" => ExecuteDrawLine(toolCall),
                "draw_polyline" => ExecuteDrawPolyline(toolCall),
                "draw_objects" => ExecuteDrawObjects(toolCall),
                "get_model_objects" => ExecuteGetModelObjects(),
                "delete_objects" => ExecuteDeleteObjects(toolCall),
                "execute_lisp" => ExecuteLisp(toolCall),
                "read_lisp_output" => ExecuteReadLispOutput(),
                "get_polyline_vertices" => ExecuteGetPolylineVertices(),
                _ => $"Неизвестный инструмент: {toolCall.Name}"
            };
        }
        catch (Exception ex)
        {
            return $"Ошибка инструмента {toolCall.Name}: {ex.Message}";
        }
    }

    private string ExecuteDrawCircle(LlmToolCall toolCall)
    {
        var radius = toolCall.Arguments.GetDouble("radius");
        var centerX = toolCall.Arguments.GetDouble("center_x");
        var centerY = toolCall.Arguments.GetDouble("center_y");
        var result = commandExecutor.DrawCircle(centerX, centerY, radius);
        return FormatResult(result);
    }

    private string ExecuteDrawLine(LlmToolCall toolCall)
    {
        var startX = toolCall.Arguments.GetDouble("start_x");
        var startY = toolCall.Arguments.GetDouble("start_y");
        var endX = toolCall.Arguments.GetDouble("end_x");
        var endY = toolCall.Arguments.GetDouble("end_y");
        var result = commandExecutor.DrawLine(startX, startY, endX, endY);
        return FormatResult(result);
    }

    private string ExecuteDrawPolyline(LlmToolCall toolCall)
    {
        if (!toolCall.Arguments.TryGetProperty("vertices", out var verticesElement) || verticesElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Не найден массив вершин 'vertices'.");

        var vertices = ParsePolylineVertices(verticesElement);
        if (vertices.Count < 2)
            throw new InvalidOperationException("Для построения полилинии необходимо минимум две вершины.");

        var closed = toolCall.Arguments.ReadOptionalBoolean("closed", false);
        var result = commandExecutor.DrawPolyline(vertices, closed);
        return FormatResult(result);
    }

    private string ExecuteDrawObjects(LlmToolCall toolCall)
    {
        if (!toolCall.Arguments.TryGetProperty("objects", out var objectsElement) || objectsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Не найден массив 'objects'.");

        // Пакетное построение: собираем список запросов по каждому элементу массива objects.
        var requests = new List<DrawingObjectRequest>();
        foreach (var objectElement in objectsElement.EnumerateArray())
        {
            if (objectElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Каждый элемент массива 'objects' должен быть объектом.");

            var type = objectElement.GetString("type").ToLowerInvariant();
            switch (type)
            {
                case "circle":
                    requests.Add(DrawingObjectRequest.ForCircle(
                        objectElement.GetDouble("center_x"),
                        objectElement.GetDouble("center_y"),
                        objectElement.GetDouble("radius")));
                    break;
                case "line":
                    requests.Add(DrawingObjectRequest.ForLine(
                        objectElement.GetDouble("start_x"),
                        objectElement.GetDouble("start_y"),
                        objectElement.GetDouble("end_x"),
                        objectElement.GetDouble("end_y")));
                    break;
                case "polyline":
                    if (!objectElement.TryGetProperty("vertices", out var verticesElement) || verticesElement.ValueKind != JsonValueKind.Array)
                        throw new InvalidOperationException("Полилиния должна содержать массив 'vertices'.");

                    var vertices = ParsePolylineVertices(verticesElement);
                    if (vertices.Count < 2)
                        throw new InvalidOperationException("Нужно указать минимум две вершины полилинии.");

                    var closed = objectElement.ReadOptionalBoolean("closed", false);
                    requests.Add(DrawingObjectRequest.ForPolyline(vertices, closed));
                    break;
                default:
                    throw new InvalidOperationException($"Тип объекта '{type}' не поддерживается.");
            }
        }

        var result = commandExecutor.DrawObjects(requests);
        return FormatResult(result);
    }

    private string ExecuteGetModelObjects()
    {
        var result = commandExecutor.GetModelObjects();
        return FormatResult(result);
    }

    private string ExecuteDeleteObjects(LlmToolCall toolCall)
    {
        if (!toolCall.Arguments.TryGetProperty("object_ids", out var idsElement) || idsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ожидался массив 'object_ids'.");

        // Приводим Id к строковому виду — исполнитель работает с идентификаторами в форме строки.
        var ids = new List<string>();
        foreach (var idElement in idsElement.EnumerateArray())
        {
            if (idElement.ValueKind == JsonValueKind.String)
            {
                var id = idElement.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id.Trim());
            }
            else if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt64(out var numericId))
            {
                ids.Add(numericId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                throw new InvalidOperationException("Идентификаторы объектов должны быть строками или числами.");
            }
        }

        var result = commandExecutor.DeleteObjects(ids);
        return FormatResult(result);
    }

    private string ExecuteLisp(LlmToolCall toolCall)
    {
        var code = toolCall.Arguments.GetString("code");
        var result = commandExecutor.ExecuteLisp(code);
        return FormatResult(result);
    }

    private string ExecuteReadLispOutput()
    {
        var result = commandExecutor.ReadLispOutput();
        return FormatResult(result);
    }

    private string ExecuteGetPolylineVertices()
    {
        var result = commandExecutor.GetPolylineVertices();
        return FormatResult(result);
    }

    // Преобразует JSON-описание вершин в коллекцию доменных объектов с учётом bulge.
    private static List<PolylineVertex> ParsePolylineVertices(JsonElement verticesElement)
    {
        var vertices = new List<PolylineVertex>();
        foreach (var vertexElement in verticesElement.EnumerateArray())
        {
            if (vertexElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Каждая вершина должна быть объектом с координатами.");

            var x = vertexElement.GetDouble("x");
            var y = vertexElement.GetDouble("y");
            var bulge = 0d;

            if (vertexElement.TryGetProperty("bulge", out var bulgeElement))
            {
                bulge = bulgeElement.ValueKind switch
                {
                    JsonValueKind.Number => bulgeElement.GetDouble(),
                    JsonValueKind.String => double.Parse(bulgeElement.GetString() ?? string.Empty, CultureInfo.InvariantCulture),
                    JsonValueKind.Null or JsonValueKind.Undefined => 0,
                    _ => throw new InvalidOperationException("Некорректный формат параметра 'bulge'.")
                };
            }

            vertices.Add(new PolylineVertex(x, y, bulge));
        }

        return vertices;
    }

    // Унифицированный формат ответа: текст + JSON (если есть), чтобы LLM и пользователь видели детали.
    private static string FormatResult(CommandExecutionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Data))
            return result.Message;

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.Message))
            builder.AppendLine(result.Message);

        builder.AppendLine("```json");
        builder.AppendLine(result.Data);
        builder.Append("```");
        return builder.ToString();
    }

    private static LlmToolDefinition DrawCircleToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "center_x", "center_y", "radius" },
            properties = new
            {
                center_x = new { type = "number", description = "Координата X центра окружности" },
                center_y = new { type = "number", description = "Координата Y центра окружности" },
                radius = new { type = "number", minimum = 0.0001, description = "Радиус окружности" }
            }
        });

        return new LlmToolDefinition(
            "draw_circle",
            "Построить круг в чертеже AutoCAD по переданным координатам и радиусу.",
            schema);
    }

    private static LlmToolDefinition DrawLineToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "start_x", "start_y", "end_x", "end_y" },
            properties = new
            {
                start_x = new { type = "number", description = "Координата X начальной точки" },
                start_y = new { type = "number", description = "Координата Y начальной точки" },
                end_x = new { type = "number", description = "Координата X конечной точки" },
                end_y = new { type = "number", description = "Координата Y конечной точки" }
            }
        });

        return new LlmToolDefinition(
            "draw_line",
            "Построить отрезок между двумя точками в чертеже AutoCAD.",
            schema);
    }

    private static LlmToolDefinition DrawPolylineToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "vertices" },
            properties = new
            {
                vertices = new
                {
                    type = "array",
                    minItems = 2,
                    description = "Список вершин полилинии с координатами и необязательным параметром bulge.",
                    items = new
                    {
                        type = "object",
                        required = new[] { "x", "y" },
                        properties = new
                        {
                            x = new { type = "number", description = "Координата X вершины" },
                            y = new { type = "number", description = "Координата Y вершины" },
                            bulge = new { type = "number", description = "Опциональный коэффициент bulge" }
                        }
                    }
                },
                closed = new { type = "boolean", description = "Замкнуть полилинию после добавления всех вершин." }
            }
        });

        return new LlmToolDefinition(
            "draw_polyline",
            "Построить полилинию по списку вершин. Поддерживается параметр bulge и режим замыкания.",
            schema);
    }

    private static LlmToolDefinition DrawObjectsToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "objects" },
            properties = new
            {
                objects = new
                {
                    type = "array",
                    minItems = 1,
                    description = "Список объектов для построения.",
                    items = new
                    {
                        type = "object",
                        required = new[] { "type" },
                        properties = new
                        {
                            type = new { type = "string", description = "Тип объекта: circle, line или polyline." },
                            center_x = new { type = "number" },
                            center_y = new { type = "number" },
                            radius = new { type = "number" },
                            start_x = new { type = "number" },
                            start_y = new { type = "number" },
                            end_x = new { type = "number" },
                            end_y = new { type = "number" },
                            vertices = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    required = new[] { "x", "y" },
                                    properties = new
                                    {
                                        x = new { type = "number" },
                                        y = new { type = "number" },
                                        bulge = new { type = "number" }
                                    }
                                }
                            },
                            closed = new { type = "boolean" }
                        }
                    }
                }
            }
        });

        return new LlmToolDefinition(
            "draw_objects",
            "Построить список объектов (круги, отрезки, полилинии) в одном вызове.",
            schema);
    }

    private static LlmToolDefinition GetObjectsToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new { });

        return new LlmToolDefinition(
            "get_model_objects",
            "Вернуть список объектов чертежа: круги, отрезки и полилинии.",
            schema);
    }

    private static LlmToolDefinition DeleteObjectsToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "object_ids" },
            properties = new
            {
                object_ids = new
                {
                    type = "array",
                    minItems = 1,
                    description = "Список идентификаторов объектов для удаления.",
                    items = new { type = "string" }
                }
            }
        });

        return new LlmToolDefinition(
            "delete_objects",
            "Удалить объекты по их идентификаторам.",
            schema);
    }

    private static LlmToolDefinition ExecuteLispToolDefinition()
    {
        // Описание инструмента формируется в формате JSON Schema (OpenAI function calling).
        // Поле type задаёт тип корневого объекта, required перечисляет обязательные свойства,
        // а секция properties описывает каждый аргумент (тип значения, текст подсказки и т.п.).
        // LLM, увидев такую схему, знает, какие параметры ей нужно запросить у пользователя
        // перед вызовом инструмента execute_lisp.
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "code" },
            properties = new
            {
                code = new
                {
                    type = "string",
                    description = "AutoLISP-код, который нужно выполнить. При необходимости оборачивай команды в (progn ...). Возвращается результат выражения."
                }
            }
        });

        return new LlmToolDefinition(
            "execute_lisp",
            "Выполнить произвольный AutoLISP-код. После завершения вызови read_lisp_output, чтобы получить результат.",
            schema);
    }

    private static LlmToolDefinition ReadLispOutputToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new { });

        return new LlmToolDefinition(
            "read_lisp_output",
            "Прочитать содержимое lisp-output.txt (результат последнего запуска LISP). Повтори вызов, если файл ещё не создан.",
            schema);
    }

    private static LlmToolDefinition GetPolylineToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new { });

        return new LlmToolDefinition(
            "get_polyline_vertices",
            "Получить координаты вершин выбранной полилинии и вернуть их в формате JSON.",
            schema);
    }

    private IReadOnlyList<LlmMessage> TrimHistory()
    {
        const int maxMessages = 30;
        if (_history.Count <= maxMessages)
            return _history;

        return _history.GetRange(_history.Count - maxMessages, maxMessages);
    }
}