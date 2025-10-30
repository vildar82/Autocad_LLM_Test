using CommunityToolkit.Mvvm.ComponentModel;

namespace AutocadMcpPlugin.UI.ViewModels;

/// <summary>
/// Представляет одно сообщение в истории чата.
/// </summary>
public sealed partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _author;

    [ObservableProperty]
    private string _text;

    public ChatMessageViewModel(string author, string text)
    {
        _author = author;
        _text = text;
    }
}
