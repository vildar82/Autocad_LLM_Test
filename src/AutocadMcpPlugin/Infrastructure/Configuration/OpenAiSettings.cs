using System;

namespace AutocadMcpPlugin.Infrastructure.Configuration;

/// <summary>
/// Настройки подключения к OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string DefaultModel { get; set; } = "gpt-4o-mini";

    public double Temperature { get; set; } = 0.2;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("API-ключ OpenAI не задан.");

        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("Не указан базовый URL OpenAI API.");

        if (string.IsNullOrWhiteSpace(DefaultModel))
            throw new InvalidOperationException("Не указано имя модели OpenAI.");
    }
}