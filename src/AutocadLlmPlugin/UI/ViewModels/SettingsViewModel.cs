using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutocadLlmPlugin.UI.ViewModels;

/// <summary>
/// ViewModel окна настроек.
/// </summary>
public sealed partial class SettingsViewModel(ISettingsService settingsService) : ObservableObject
{
    [ObservableProperty]
    private string _openAiApiKey = settingsService.Current.OpenAiApiKey;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private void Save()
    {
        settingsService.SetOpenAiApiKey(OpenAiApiKey);
        StatusMessage = "Сохранено.";
    }
}