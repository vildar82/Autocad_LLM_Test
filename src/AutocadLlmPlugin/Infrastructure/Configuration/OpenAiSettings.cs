namespace AutocadLlmPlugin.Infrastructure.Configuration;

/// <summary>
/// Настройки подключения к OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string DefaultModel { get; set; } = "o4-mini";

    public double Temperature { get; set; } = 1;
}