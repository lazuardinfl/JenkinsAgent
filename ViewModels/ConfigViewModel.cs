using Bot.Helpers;
using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bot.ViewModels;

public partial class ConfigViewModel : PageViewModelBase
{
    private readonly Config config;

    [ObservableProperty]
    private string? orchestratorUrl;

    [ObservableProperty]
    private string? botId;

    [ObservableProperty]
    private string? botToken;

    public ConfigViewModel(Config config)
    {
        this.config = config;
        config.Changed += OnConfigChanged;
        Initialize();
    }

    private async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        SetValueOnUI();
    } 

    private void SetValueOnUI()
    {
        OrchestratorUrl = config.Client.OrchestratorUrl;
        BotId = config.Client.BotId;
        BotToken = config.Client.BotToken;
    }

    private void OnConfigChanged(object? sender, EventArgs e) => SetValueOnUI();

    [RelayCommand]
    private async Task Apply()
    {
        if (DialogResult.Yes == await MessageBoxHelper.ShowQuestionYesNoAsync("Save Config", "Are you sure to apply bot config?"))
        {
            config.Client.OrchestratorUrl = OrchestratorUrl;
            config.Client.BotId = BotId;
            config.Client.BotToken = BotToken;
            Hide();
            await config.Save();
            await config.Reload();
            config.RaiseChanged(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Close()
    {
        Hide();
        SetValueOnUI();
    }
}