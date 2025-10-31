using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutocadMcpPlugin;
using AutocadMcpPlugin.Infrastructure.Configuration;

namespace AutocadMcpPlugin.Application.Llm;

/// <summary>
/// Клиент OpenAI Chat Completions.
/// </summary>
public sealed class OpenAiLlmClient : ILlmClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly bool _disposeClient;

    public OpenAiLlmClient(OpenAiSettings settings, HttpClient? httpClient = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        if (httpClient == null)
        {
            _httpClient = new HttpClient();
            _disposeClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }

        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey) &&
            _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
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

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
            var payload = new
            {
                model = request.Model ?? _settings.DefaultModel,
                temperature = request.Temperature ?? _settings.Temperature,
                messages = request.Messages.Select(m => new { role = m.Role, content = m.Content })
            };

            using var httpContent = JsonContent.Create(payload);
            using var response = await _httpClient.PostAsync("chat/completions", httpContent, cancellationToken);

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var error = TryExtractError(body);
                return LlmChatResult.Failure($"OpenAI вернул ошибку: {error}");
            }

            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
            var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                return LlmChatResult.Failure("OpenAI вернул пустой ответ.");

            return LlmChatResult.Success(content!);
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

    private static string TryExtractError(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<OpenAiErrorResponse>(body, JsonOptions);
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