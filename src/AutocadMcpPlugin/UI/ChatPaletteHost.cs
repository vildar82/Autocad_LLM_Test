using System;
using Autodesk.AutoCAD.Windows;
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

    private void OnSendRequested(object? sender, string message)
    {
        // Простая заглушка до внедрения логики LLM/MCP.
        _control.AppendMessage("Ассистент", $"Получено сообщение: {message}");
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