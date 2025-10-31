using System;
using AutocadMcpPlugin.Infrastructure.DependencyInjection;
using AutocadMcpPlugin.UI.Controls;
using AutocadMcpPlugin.UI.ViewModels;
using AutocadMcpPlugin.UI.Windows;
using Autodesk.AutoCAD.Windows;

namespace AutocadMcpPlugin.UI;

/// <summary>
/// Управляет жизненным циклом палитры чата внутри AutoCAD.
/// </summary>
public sealed class ChatPaletteHost : IDisposable
{
    private static ChatPaletteHost? _instance;
    private readonly PaletteSet _paletteSet;
    private readonly ChatPaletteControl _control;
    private SettingsWindow? _settingsWindow;
    private bool _disposed;

    private ChatPaletteHost()
    {
        _control = new ChatPaletteControl
        {
            ViewModel = PluginServiceProvider.GetRequiredService<ChatViewModel>()
        };
        _control.SettingsRequested += OnSettingsRequested;

        _paletteSet = new PaletteSet("MCP Assistant")
        {
            KeepFocus = false,
            Style = PaletteSetStyles.ShowAutoHideButton |
                    PaletteSetStyles.ShowCloseButton |
                    PaletteSetStyles.ShowPropertiesMenu
        };

        _paletteSet.AddVisual("Chat", _control);
    }

    public static ChatPaletteHost Instance
    {
        get
        {
            return _instance ??= new ChatPaletteHost();
        }
    }

    public void Show()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ChatPaletteHost));

        _paletteSet.Visible = true;
    }

    public static void DisposeInstance()
    {
        _instance?.DisposeCore();
        _instance = null;
    }

    public void Dispose() => DisposeCore();

    private void DisposeCore()
    {
        if (_disposed)
            return;

        _disposed = true;
        _control.SettingsRequested -= OnSettingsRequested;
        _control.ViewModel = null;
        _paletteSet.Dispose();

        if (_settingsWindow != null)
        {
            _settingsWindow.Close();
            _settingsWindow = null;
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_disposed)
            return;

        if (_settingsWindow == null)
        {
            var viewModel = PluginServiceProvider.GetRequiredService<SettingsViewModel>();
            _settingsWindow = new SettingsWindow
            {
                DataContext = viewModel,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }
}
