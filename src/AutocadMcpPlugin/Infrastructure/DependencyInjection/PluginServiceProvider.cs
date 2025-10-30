using System;
using AutocadMcpPlugin.Application.Conversations;
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

    /// <summary>
    /// Доступ к построенному контейнеру.
    /// </summary>
    private static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Контейнер зависимостей плагина не инициализирован.");

    private static bool IsInitialized => _serviceProvider is not null;

    /// <summary>
    /// Запускает инициализацию контейнера и регистрирует сервисы.
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
        // Координатор диалога пока работает как заглушка, далее подключим связку LLM/MCP.
        services.AddSingleton<IConversationCoordinator, ConversationCoordinator>();

        // ViewModel палитры чата регистрируем в контейнере, чтобы переиспользовать состояние.
        services.AddSingleton<ChatViewModel>();
    }
}