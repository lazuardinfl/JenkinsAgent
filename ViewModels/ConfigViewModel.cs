using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace Bot.ViewModels;

public partial class ConfigViewModel : PageViewModelBase
{
    private readonly Jenkins jenkins;

    [ObservableProperty]
    private string? orchestratorUrl;

    [ObservableProperty]
    private string? botId;

    [ObservableProperty]
    private string? botToken;

    public ConfigViewModel(Jenkins jenkins)
    {
        this.jenkins = jenkins;
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
    private void Apply()
    {
        Hide();
        jenkins.Credential.Url = OrchestratorUrl;
        jenkins.Credential.Id = BotId;
        jenkins.Credential.Token = BotToken;
    }

    [RelayCommand]
    private void Close()
    {
        Hide();
        SetValueOnUI();
    }
}