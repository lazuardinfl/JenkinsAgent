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
    private readonly Jenkins jenkins;
    private readonly Func<Task<bool>> save;

    [ObservableProperty]
    private string? orchestratorUrl;

    [ObservableProperty]
    private string? botId;

    [ObservableProperty]
    private string? botToken;

    public ConfigViewModel(Jenkins jenkins, Func<Task<bool>> save)
    {
        this.jenkins = jenkins;
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
        OrchestratorUrl = jenkins.Credential.Url;
        BotId = jenkins.Credential.Id;
        BotToken = jenkins.Credential.Token;
    }

    [RelayCommand]
    private async Task Apply()
    {
        ButtonResult result = await MessageBox.QuestionOkCancel("Save config", "Are you sure to apply bot config?").ShowAsync();
        if (result == ButtonResult.Ok)
        {
            jenkins.Credential.Url = OrchestratorUrl;
            jenkins.Credential.Id = BotId;
            jenkins.Credential.Token = BotToken;
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