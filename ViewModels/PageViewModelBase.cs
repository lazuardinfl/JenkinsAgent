using CommunityToolkit.Mvvm.Input;

namespace Bot.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    [RelayCommand]
    protected static void Hide() => App.Lifetime().MainWindow!.Hide();
}