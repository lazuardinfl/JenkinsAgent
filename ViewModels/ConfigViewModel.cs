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
        ButtonResult result = await MessageBox.QuestionYesNo("Save config", "Are you sure to apply bot config?").ShowAsync();
        if (result == ButtonResult.Yes)
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