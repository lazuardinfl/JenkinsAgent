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
        config.Reloaded += OnConfigReloaded;
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

    private void OnConfigReloaded(object? sender, EventArgs e) => SetValueOnUI();

    [RelayCommand]
    private async Task Apply()
    {
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Save Config", "Are you sure to apply bot config?"))
        {
            OrchestratorUrl = Helper.CreateUrl(OrchestratorUrl);
            config.Client.OrchestratorUrl = OrchestratorUrl;
            config.Client.BotId = BotId;
            if (config.Client.BotToken != BotToken)
            {
                BotToken = Helper.RemoveWhitespaces(BotToken ?? "");
                BotToken = DataProtectionHelper.EncryptDataAsText(BotToken, DataProtectionHelper.Base64Encode(BotId));
                config.Client.BotToken = BotToken;
            }
            Hide();
            await config.Save();
            await config.Reload(true);
        }
    }

    [RelayCommand]
    private void Close()
    {
        Hide();
        SetValueOnUI();
    }
}