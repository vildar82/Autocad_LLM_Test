using AutocadMcpPlugin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutocadMcpPlugin.UI.ViewModels;

/// <summary>
/// ViewModel окна настроек.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _openAiApiKey = settingsService.Current.OpenAiApiKey;
    }

    [ObservableProperty]
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private void Save()
    {
        _settingsService.SetOpenAiApiKey(OpenAiApiKey ?? string.Empty);
        StatusMessage = "Сохранено.";
    }
}
