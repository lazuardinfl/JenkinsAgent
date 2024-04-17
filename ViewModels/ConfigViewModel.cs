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

    [ObservableProperty]
    private string? orchestratorUrl;

    [ObservableProperty]
    private string? botId;

    [ObservableProperty]
    private string? botToken;

    public ConfigViewModel(Config config, Func<Task<bool>> save)
    {
        this.config = config;
        this.save = save;
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
        ButtonResult result = await MessageBox.QuestionOkCancel("Save config", "Are you sure to apply bot config?").ShowAsync();
        if (result == ButtonResult.Ok)
        {
            config.Client.OrchestratorUrl = OrchestratorUrl;
            config.Client.BotId = BotId;
            config.Client.BotToken = BotToken;
            await save.Invoke();
            Hide();
        }
    }

    [RelayCommand]
    private void Close()
    {
        Hide();
        SetValueOnUI();
    }
}