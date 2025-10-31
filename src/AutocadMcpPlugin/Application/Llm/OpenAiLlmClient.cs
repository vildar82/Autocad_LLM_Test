using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AutocadMcpPlugin.Infrastructure.Configuration;

namespace AutocadMcpPlugin.Application.Llm;

/// <summary>
/// Клиент OpenAI Chat Completions с поддержкой вызова инструментов.
/// </summary>
public sealed class OpenAiLlmClient : ILlmClient, IDisposable
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly bool _disposeClient;

    public OpenAiLlmClient(OpenAiSettings settings, HttpClient? httpClient = null)
    {
        _settings = settings;

        if (httpClient == null)
        {
            _httpClient = new HttpClient();
            _disposeClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }

        var baseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey) &&
            _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        if (_httpClient.DefaultRequestHeaders.Accept.All(h => h.MediaType != "application/json"))
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<LlmChatResult> CreateChatCompletionAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.Messages.Count == 0)
            return LlmChatResult.Failure("Запрос к LLM должен содержать хотя бы одно сообщение.");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return LlmChatResult.Failure("API-ключ OpenAI не задан.");

        _httpClient.DefaultRequestHeaders.Authorization ??= new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
            var payload = new
            {
                model = request.Model ?? _settings.DefaultModel,
                temperature = request.Temperature ?? _settings.Temperature,
                messages = request.Messages.Select(BuildMessagePayload).ToArray(),
                tools = request.Tools.Count == 0
                    ? null
                    : request.Tools.Select(t => new
                    {
                        type = "function",
                        function = new
                        {
                            name = t.Name,
                            description = t.Description,
                            parameters = t.Parameters
                        }
                    }).ToArray()
            };

            var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            using var httpContent = JsonContent.Create(payload, options: serializerOptions);
            using var response = await _httpClient.PostAsync("chat/completions", httpContent, cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var error = TryExtractError(body);
                return LlmChatResult.Failure($"OpenAI вернул ошибку: {error}");
            }

            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(body, ResponseJsonOptions);
            var message = chatResponse?.Choices?.FirstOrDefault()?.Message;
            if (message == null)
                return LlmChatResult.Failure("OpenAI вернул пустой ответ.");

            var toolCalls = ExtractToolCalls(message.ToolCalls);

            if (toolCalls.Count > 0)
            {
                if (!string.IsNullOrEmpty(message.Content))
                    return LlmChatResult.Success(message.Content!, toolCalls);

                return LlmChatResult.ToolCallsOnly(toolCalls);
            }

            if (string.IsNullOrWhiteSpace(message.Content))
                return LlmChatResult.Failure("OpenAI вернул пустой ответ.");

            return LlmChatResult.Success(message.Content!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LlmChatResult.Failure($"Не удалось выполнить запрос к OpenAI: {ex.Message}");
        }
    }

    private object BuildMessagePayload(LlmMessage message)
    {
        var toolCalls = message.ToolCalls.Count == 0
            ? null
            : message.ToolCalls.Select(tc => new
            {
                id = tc.Id,
                type = "function",
                function = new
                {
                    name = tc.Name,
                    arguments = tc.Arguments.GetRawText()
                }
            }).ToArray();

        return new
        {
            role = message.Role,
            content = message.Content,
            tool_call_id = message.ToolCallId,
            tool_calls = toolCalls
        };
    }

    private static IReadOnlyList<LlmToolCall> ExtractToolCalls(ToolCallDto[]? toolCalls)
    {
        if (toolCalls == null || toolCalls.Length == 0)
            return [];

        var result = new List<LlmToolCall>(toolCalls.Length);

        foreach (var call in toolCalls)
        {
            var function = call.Function;
            if (function == null)
                continue;

            var name = (function.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            var arguments = ParseArguments(function.Arguments);
            result.Add(new LlmToolCall(call.Id ?? string.Empty, name, arguments));
        }

        return result;
    }

    private static JsonElement ParseArguments(string? arguments)
    {
        var json = string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments!;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string TryExtractError(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<OpenAiErrorResponse>(body, ResponseJsonOptions);
            var message = error?.Error?.Message;
            return string.IsNullOrWhiteSpace(message)
                ? body
                : message!;
        }
        catch
        {
            return body;
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
            _httpClient.Dispose();
    }

    private sealed class ChatCompletionResponse
    {
        public Choice[]? Choices { get; set; }
    }

    private sealed class Choice
    {
        public ChoiceMessage? Message { get; set; }
    }

    private sealed class ChoiceMessage
    {
        public string? Role { get; set; }

        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public ToolCallDto[]? ToolCalls { get; set; }
    }

    private sealed class ToolCallDto
    {
        public string? Id { get; set; }

        public string? Type { get; set; }

        [JsonPropertyName("function")]
        public ToolFunctionDto? Function { get; set; }
    }

    private sealed class ToolFunctionDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }

    private sealed class OpenAiErrorResponse
    {
        public OpenAiError? Error { get; set; }
    }

    private sealed class OpenAiError
    {
        public string? Message { get; set; }
    }
}