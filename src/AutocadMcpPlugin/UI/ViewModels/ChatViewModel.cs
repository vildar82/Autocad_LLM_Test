using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AutocadMcpPlugin.Application.Conversations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutocadMcpPlugin.UI.ViewModels;

/// <summary>
/// ViewModel палитры чата.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly IConversationCoordinator _conversationCoordinator;

    public ChatViewModel(IConversationCoordinator conversationCoordinator)
    {
        _conversationCoordinator = conversationCoordinator;
        Messages = [];
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _userInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage = "Готов к работе.";

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var message = UserInput.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Обрабатываю запрос…";

            Messages.Add(new ChatMessageViewModel("Вы", message));
            UserInput = string.Empty;

            var response = await _conversationCoordinator.ProcessUserMessageAsync(message);
            Messages.Add(new ChatMessageViewModel("Ассистент", response));

            StatusMessage = "Готово.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel("Система", $"Ошибка: {ex.Message}"));
            StatusMessage = "Возникла ошибка.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(UserInput);
    }
}
