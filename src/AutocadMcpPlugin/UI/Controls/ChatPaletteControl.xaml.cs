using System;
using System.Windows;
using System.Windows.Controls;
using AutocadMcpPlugin.UI.ViewModels;

namespace AutocadMcpPlugin.UI.Controls;

/// <summary>
/// Пользовательский контроль палитры чата.
/// </summary>
public partial class ChatPaletteControl : UserControl
{
    public ChatPaletteControl()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingsRequested;

    public ChatViewModel? ViewModel
    {
        get => DataContext as ChatViewModel;
        set => DataContext = value;
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
