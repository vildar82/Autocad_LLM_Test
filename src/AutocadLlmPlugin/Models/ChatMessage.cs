using CommunityToolkit.Mvvm.ComponentModel;

namespace AutocadLlmPlugin;

/// <summary>
/// Представляет одно сообщение в истории чата.
/// </summary>
public sealed partial class ChatMessage(string author, string text) : ObservableObject
{
    [ObservableProperty]
    private string _author = author;

    [ObservableProperty]
    private string _text = text;
}