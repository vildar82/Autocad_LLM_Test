namespace AutocadLlmPlugin;

/// <summary>
/// Настройки плагина, сохраняемые на диске.
/// </summary>
public sealed class PluginSettings
{
    public string OpenAiApiKey { get; set; } = string.Empty;
}
