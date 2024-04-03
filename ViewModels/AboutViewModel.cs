using CommunityToolkit.Mvvm.ComponentModel;

namespace Bot.ViewModels;

public partial class AboutViewModel : PageViewModelBase
{
    [ObservableProperty]
    private string? name = App.Description;

    [ObservableProperty]
    private string? version = $"Version {App.Version?.Major}.{App.Version?.Minor}.{App.Version?.Build}";
}