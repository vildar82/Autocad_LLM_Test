using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutocadMcpPlugin.UI.ViewModels;

/// <summary>
/// ViewModel палитры чата.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly IConversationCoordinator _conversationCoordinator;
    private readonly ISettingsService _settingsService;

    public ChatViewModel(IConversationCoordinator conversationCoordinator, ISettingsService settingsService)
    {
        _conversationCoordinator = conversationCoordinator;
        _settingsService = settingsService;
        Messages = [];

        UpdateIdleStatus();
        _settingsService.SettingsSaved += OnSettingsSaved;
    }

    public ObservableCollection<ChatMessage> Messages { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _userInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var message = UserInput.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Обрабатываю запрос...";

            Messages.Add(new ChatMessage("Вы", message));
            UserInput = string.Empty;

            var response = await _conversationCoordinator.ProcessUserMessageAsync(message);
            Messages.Add(new ChatMessage("Ассистент", response));

            UpdateIdleStatus();
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage("Система", $"Ошибка: {ex.Message}"));
            StatusMessage = "Возникла ошибка.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(UserInput);

    private void OnSettingsSaved(object? sender, PluginSettings settings) => UpdateIdleStatus();

    private void UpdateIdleStatus()
    {
        StatusMessage = string.IsNullOrWhiteSpace(_settingsService.Current.OpenAiApiKey)
            ? "LLM: укажите API-ключ в настройках."
            : "Готов к работе.";
    }
}