using System;
using System.Net.Http;
using AutocadMcpPlugin;
using AutocadMcpPlugin.Application.Commands;
using AutocadMcpPlugin.Application.Conversations;
using AutocadMcpPlugin.Application.Llm;
using AutocadMcpPlugin.Infrastructure.Configuration;
using AutocadMcpPlugin.Infrastructure.Settings;
using AutocadMcpPlugin.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AutocadMcpPlugin.Infrastructure.DependencyInjection;

/// <summary>
/// Управляет жизненным циклом контейнера зависимостей плагина.
/// </summary>
public static class PluginServiceProvider
{
    private static ServiceProvider? _serviceProvider;
    private static readonly object SyncRoot = new();

    private static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Контейнер зависимостей плагина не инициализирован.");

    private static bool IsInitialized => _serviceProvider is not null;

    /// <summary>
    /// Выполняет инициализацию контейнера и регистрирует сервисы.
    /// </summary>
    public static void Initialize()
    {
        if (IsInitialized)
            return;

        lock (SyncRoot)
        {
            if (IsInitialized)
                return;

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// Освобождает ресурсы контейнера.
    /// </summary>
    public static void Dispose()
    {
        lock (SyncRoot)
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }
    }

    /// <summary>
    /// Унифицированный способ получения сервисов.
    /// </summary>
    public static T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        services.AddSingleton(provider =>
        {
            var settingsService = (JsonSettingsService)provider.GetRequiredService<ISettingsService>();
            var openAiSettings = new OpenAiSettings
            {
                ApiKey = settingsService.Current.OpenAiApiKey
            };

            settingsService.SettingsSaved += (_, updatedSettings) =>
            {
                openAiSettings.ApiKey = updatedSettings.OpenAiApiKey;
            };

            return openAiSettings;
        });

        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        });

        services.AddSingleton<ILlmClient, OpenAiLlmClient>();
        services.AddSingleton<IAutocadCommandExecutor, AutocadCommandExecutor>();
        services.AddSingleton<IConversationCoordinator, ConversationCoordinator>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
}