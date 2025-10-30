using Autodesk.AutoCAD.Runtime;
using AutocadMcpPlugin.UI;

namespace AutocadMcpPlugin.Plugin;

/// <summary>
/// Точка входа плагина AutoCAD.
/// </summary>
public sealed class PluginEntry : IExtensionApplication
{
    // TODO: заменить на полноценный логгер после интеграции с инфраструктурой.

    public void Initialize()
    {
        System.Diagnostics.Debug.WriteLine("MCP плагин инициализирован.");
    }

    public void Terminate()
    {
        ChatPaletteHost.DisposeInstance();
        System.Diagnostics.Debug.WriteLine("MCP плагин выгружен.");
    }
}

/// <summary>
/// Базовая коллекция команд плагина.
/// </summary>
public static class PluginCommands
{
    [CommandMethod("MCPCHAT")]
    public static void LaunchChat()
    {
        ChatPaletteHost.Instance.Show();
    }
}
