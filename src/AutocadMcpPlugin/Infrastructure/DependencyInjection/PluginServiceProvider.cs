using System;
using Microsoft.Extensions.DependencyInjection;
using AutocadMcpPlugin.Application.Conversations;

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
    public static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Контейнер зависимостей плагина не инициализирован.");

    public static bool IsInitialized => _serviceProvider is not null;

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
        // Регистрация координатора диалога (заглушка, будет расширяться по мере внедрения MCP/LLM).
        services.AddSingleton<IConversationCoordinator, ConversationCoordinator>();
    }
}