using System;
using Autodesk.AutoCAD.Windows;
using AutocadMcpPlugin.Application.Conversations;
using AutocadMcpPlugin.Infrastructure.DependencyInjection;
using AutocadMcpPlugin.UI.Controls;

namespace AutocadMcpPlugin.UI;

/// <summary>
/// Управляет жизненным циклом палитры чата внутри AutoCAD.
/// </summary>
public sealed class ChatPaletteHost : IDisposable
{
    private static ChatPaletteHost? _instance;
    private readonly PaletteSet _paletteSet;
    private readonly ChatPaletteControl _control;
    private bool _disposed;

    private ChatPaletteHost()
    {
        _control = new ChatPaletteControl();
        _control.SendRequested += OnSendRequested;

        _paletteSet = new PaletteSet("MCP Assistant")
        {
            KeepFocus = false,
            Style = PaletteSetStyles.ShowAutoHideButton |
                    PaletteSetStyles.ShowCloseButton |
                    PaletteSetStyles.ShowPropertiesMenu
        };

        _paletteSet.AddVisual("Chat", _control);
    }

    public static ChatPaletteHost Instance => _instance ??= new ChatPaletteHost();

    public void Show()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ChatPaletteHost));

        _paletteSet.Visible = true;
    }

    private async void OnSendRequested(object? sender, string message)
    {
        if (_disposed)
            return;

        try
        {
            var coordinator = PluginServiceProvider.GetRequiredService<IConversationCoordinator>();
            var response = await coordinator.ProcessUserMessageAsync(message);

            _control.AppendMessage("Ассистент", response);
        }
        catch (Exception ex)
        {
            _control.AppendMessage("Система", $"Ошибка: {ex.Message}");
        }
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
        _control.SendRequested -= OnSendRequested;
        _paletteSet.Dispose();
    }
}