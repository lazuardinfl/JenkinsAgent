using Bot.Models;
using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using System;
using System.Threading.Tasks;

namespace Bot.ViewModels;

public partial class ConfigViewModel : PageViewModelBase
{
    private readonly Config config;
    private readonly Func<Task<bool>> save;
    private readonly Func<bool, Task<bool>> reload;

    [ObservableProperty]
    private string? orchestratorUrl;

    [ObservableProperty]
    private string? botId;

    [ObservableProperty]
    private string? botToken;

    public ConfigViewModel(Config config, Agent agent)
    {
        this.config = config;
        save = agent.SaveConfig;
        reload = agent.ReloadConnection;
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

    [RelayCommand]
    private async Task Apply()
    {
        ButtonResult result = await MessageBox.QuestionYesNo("Save config", "Are you sure to apply bot config?").ShowAsync();
        if (result == ButtonResult.Yes)
        {
            config.Client.OrchestratorUrl = OrchestratorUrl;
            config.Client.BotId = BotId;
            config.Client.BotToken = BotToken;
            Hide();
            await save.Invoke();
            await reload.Invoke(false);
        }
    }

    [RelayCommand]
    private void Close()
    {
        Hide();
        SetValueOnUI();
    }
}