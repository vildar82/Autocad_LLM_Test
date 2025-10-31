using System;
using System.Collections.Generic;
using System.Globalization;
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
            "Ты являешься ассистентом AutoCAD. Выполняй запросы пользователя с помощью доступных инструментов, чтобы создавать геометрию."),
        LlmMessage.CreateSystem(
            "Если параметры запроса неполные, сначала уточни недостающие данные у пользователя.")
    ];

    private readonly IReadOnlyList<LlmToolDefinition> _toolDefinitions =
    [
        CreateCircleToolDefinition(),
        CreateLineToolDefinition()
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
                    var toolResult = await ExecuteToolCallAsync(toolCall, cancellationToken);
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

    private async Task<string> ExecuteToolCallAsync(LlmToolCall toolCall, CancellationToken cancellationToken)
    {
        try
        {
            return toolCall.Name switch
            {
                "draw_circle" => await ExecuteDrawCircleAsync(toolCall, cancellationToken),
                "draw_line" => await ExecuteDrawLineAsync(toolCall, cancellationToken),
                _ => $"Неизвестный инструмент: {toolCall.Name}"
            };
        }
        catch (Exception ex)
        {
            return $"Ошибка инструмента {toolCall.Name}: {ex.Message}";
        }
    }

    private async Task<string> ExecuteDrawCircleAsync(LlmToolCall toolCall, CancellationToken cancellationToken)
    {
        var radius = GetDouble(toolCall.Arguments, "radius");
        var centerX = GetDouble(toolCall.Arguments, "center_x");
        var centerY = GetDouble(toolCall.Arguments, "center_y");
        var result = await commandExecutor.DrawCircleAsync(centerX, centerY, radius, cancellationToken);
        return result.Message;
    }

    private async Task<string> ExecuteDrawLineAsync(LlmToolCall toolCall, CancellationToken cancellationToken)
    {
        var startX = GetDouble(toolCall.Arguments, "start_x");
        var startY = GetDouble(toolCall.Arguments, "start_y");
        var endX = GetDouble(toolCall.Arguments, "end_x");
        var endY = GetDouble(toolCall.Arguments, "end_y");
        var result = await commandExecutor.DrawLineAsync(startX, startY, endX, endY, cancellationToken);
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

    private static double GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            throw new InvalidOperationException($"Не найден параметр '{propertyName}'.");

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => double.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Некорректный формат параметра '{propertyName}'.")
        };
    }

    private IReadOnlyList<LlmMessage> TrimHistory()
    {
        const int maxMessages = 30;
        if (_history.Count <= maxMessages)
            return _history;

        return _history.GetRange(_history.Count - maxMessages, maxMessages);
    }
}
