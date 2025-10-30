using System;
using System.Windows;
using System.Windows.Controls;

namespace AutocadMcpPlugin.UI.Controls;

/// <summary>
/// Простая палитра чата для первоначального взаимодействия с пользователем.
/// </summary>
public partial class ChatPaletteControl : UserControl
{
    public ChatPaletteControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Событие отправки сообщения пользователем.
    /// </summary>
    public event EventHandler<string>? SendRequested;

    /// <summary>
    /// Добавляет сообщение в историю чата.
    /// </summary>
    public void AppendMessage(string author, string message)
    {
        // Простейшее форматирование сообщения.
        var textBlock = new TextBlock
        {
            Text = $"{author}: {message}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        HistoryPanel.Children.Add(textBlock);
    }

    private void OnSendClicked(object sender, RoutedEventArgs e)
    {
        var text = PromptTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        AppendMessage("Вы", text);
        PromptTextBox.Clear();
        SendRequested?.Invoke(this, text);
    }
}
