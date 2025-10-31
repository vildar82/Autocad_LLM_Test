using System;

namespace AutocadLlmPlugin;

/// <summary>
/// Сервис хранения и предоставления настроек плагина.
/// </summary>
public interface ISettingsService
{
    PluginSettings Current { get; }

    event EventHandler<PluginSettings>? SettingsSaved;

    void SetOpenAiApiKey(string apiKey);
}
