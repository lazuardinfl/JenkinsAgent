using Bot.Helpers;
using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Diagnostics;
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

    [ObservableProperty]
    private bool isUacDisabled;

    public ConfigViewModel(Config config)
    {
        this.config = config;
        config.Reloaded += OnConfigReloaded;
    }

    private static int UacRegistry
    {
        get
        {
            try
            {
                return Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "ConsentPromptBehaviorAdmin", -1) is int value ? value : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }
        set
        {
            try
            {
                using (Process process = new())
                {
                    process.StartInfo.FileName = "reg";
                    process.StartInfo.Arguments = @"add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System " +
                        $"/v ConsentPromptBehaviorAdmin /t REG_DWORD /d {value} /f";
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode is not 0)
                    {
                        throw new InvalidOperationException("Error while executing reg");
                    }
                }
            }
            catch (Exception)
            {
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.UnexpectedError));
            }
        }
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        SetValueOnUI();
    }

    private void SetValueOnUI()
    {
        OrchestratorUrl = config.Client.OrchestratorUrl;
        BotId = config.Client.BotId;
        BotToken = config.Client.BotToken;
        IsUacDisabled = UacRegistry == 0;
    }

    private void OnConfigReloaded(object? sender, EventArgs e) => SetValueOnUI();

    [RelayCommand]
    private async Task Apply()
    {
        if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Save Config", "Are you sure to apply bot config?"))
        {
            Hide();
            OrchestratorUrl = Helper.CreateUrl(OrchestratorUrl);
            config.Client.OrchestratorUrl = OrchestratorUrl;
            config.Client.BotId = BotId;
            if (config.Client.BotToken != BotToken)
            {
                BotToken = Helper.RemoveWhitespaces(BotToken ?? "");
                BotToken = CryptographyHelper.EncryptWithDPAPI(BotToken, CryptographyHelper.Base64Encode(BotId));
                config.Client.BotToken = BotToken;
            }
            int uac = UacRegistry;
            if ((IsUacDisabled && (uac != 0)) || (!IsUacDisabled && (uac == 0)))
            {
                UacRegistry = IsUacDisabled ? 0 : 5;
                IsUacDisabled = UacRegistry == 0;
            }
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
