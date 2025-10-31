using AutocadLlmPlugin.Infrastructure.DependencyInjection;
using AutocadLlmPlugin.UI;
using Autodesk.AutoCAD.Runtime;

namespace AutocadLlmPlugin.Plugin;

/// <summary>
/// Точка входа плагина AutoCAD.
/// </summary>
public sealed class PluginEntry : IExtensionApplication
{
    // TODO: заменить вывод в отладку на централизованное логирование.

    public void Initialize()
    {
        PluginServiceProvider.Initialize();
        System.Diagnostics.Debug.WriteLine("LLM плагин инициализирован.");
    }

    public void Terminate()
    {
        ChatPaletteHost.DisposeInstance();
        PluginServiceProvider.Dispose();
        System.Diagnostics.Debug.WriteLine("LLM плагин выгружен.");
    }
}

/// <summary>
/// Набор команд плагина.
/// </summary>
public static class PluginCommands
{
    [CommandMethod("LLMCHAT")]
    public static void LaunchChat()
    {
        ChatPaletteHost.Instance.Show();
    }
}