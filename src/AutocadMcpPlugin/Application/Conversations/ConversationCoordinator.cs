using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutocadMcpPlugin.Infrastructure.Configuration;

namespace AutocadMcpPlugin.Application.Conversations;

/// <summary>
/// Координатор диалога между LLM и MCP-инструментами AutoCAD.
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
        CreateCircleToolDefinition(),
        CreateLineToolDefinition(),
        GetPolylineToolDefinition()
    ];

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
            return toolCall.Name switch
            {
                "draw_circle" => ExecuteDrawCircle(toolCall),
                "draw_line" => ExecuteDrawLine(toolCall),
                "get_polyline_vertices" => ExecuteGetPolylineVertices(toolCall),
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
        return result.Message;
    }

    private string ExecuteDrawLine(LlmToolCall toolCall)
    {
        var startX = toolCall.Arguments.GetDouble("start_x");
        var startY = toolCall.Arguments.GetDouble("start_y");
        var endX = toolCall.Arguments.GetDouble("end_x");
        var endY = toolCall.Arguments.GetDouble("end_y");
        var result = commandExecutor.DrawLine(startX, startY, endX, endY);
        return result.Message;
    }

    private string ExecuteGetPolylineVertices(LlmToolCall toolCall)
    {
        var result = commandExecutor.GetPolylineVertices();
        return result.Message;
    }

    private static LlmToolDefinition CreateCircleToolDefinition()
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

    private static LlmToolDefinition CreateLineToolDefinition()
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

    private static LlmToolDefinition GetPolylineToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new { });

        return new LlmToolDefinition(
            "get_polyline_vertices",
            "Получить координаты вершин полилинии и вернуть их в формате JSON.",
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