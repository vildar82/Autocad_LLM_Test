using Autodesk.AutoCAD.Runtime;
using AutocadMcpPlugin.Infrastructure.DependencyInjection;
using AutocadMcpPlugin.UI;

namespace AutocadMcpPlugin.Plugin;

/// <summary>
/// Точка входа плагина AutoCAD.
/// </summary>
public sealed class PluginEntry : IExtensionApplication
{
    // TODO: заменить вывод в отладку на централизованное логирование.

    public void Initialize()
    {
        PluginServiceProvider.Initialize();
        System.Diagnostics.Debug.WriteLine("MCP плагин инициализирован.");
    }

    public void Terminate()
    {
        ChatPaletteHost.DisposeInstance();
        PluginServiceProvider.Dispose();
        System.Diagnostics.Debug.WriteLine("MCP плагин выгружен.");
    }
}

/// <summary>
/// Набор команд плагина.
/// </summary>
public static class PluginCommands
{
    [CommandMethod("MCPCHAT")]
    public static void LaunchChat()
    {
        ChatPaletteHost.Instance.Show();
    }
}