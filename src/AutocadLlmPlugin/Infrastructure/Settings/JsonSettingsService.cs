using System;
using System.IO;
using System.Text.Json;

namespace AutocadLlmPlugin.Infrastructure.Settings;

/// <summary>
/// Файловое хранилище настроек в формате JSON.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _settingsFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "AutocadLlmPlugin");
        Directory.CreateDirectory(directory);

        _settingsFile = Path.Combine(directory, "settings.json");
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        Current = Load();
    }

    public PluginSettings Current { get; }

    public event EventHandler<PluginSettings>? SettingsSaved;

    public void SetOpenAiApiKey(string apiKey)
    {
        Current.OpenAiApiKey = apiKey;
        Save(Current);
        SettingsSaved?.Invoke(this, Current);
    }

    private PluginSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                var settings = JsonSerializer.Deserialize<PluginSettings>(json, _jsonOptions);
                if (settings != null)
                    return settings;
            }
        }
        catch
        {
            // Игнорируем ошибки чтения и возвращаем настройки по умолчанию.
        }

        return new PluginSettings();
    }

    private void Save(PluginSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsFile, json);
        }
        catch
        {
            // Игнорируем ошибки записи, чтобы не блокировать основной функционал.
        }
    }
}