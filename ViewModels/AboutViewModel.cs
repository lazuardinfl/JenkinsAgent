namespace Bot.ViewModels;

public partial class AboutViewModel : PageViewModelBase
{
    public string Name { get; } = App.Description;

    public string Version { get; } = $"Version {App.Version}";
}
