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

    public ChatViewModel? ViewModel
    {
        get => DataContext as ChatViewModel;
        set => DataContext = value;
    }
}
