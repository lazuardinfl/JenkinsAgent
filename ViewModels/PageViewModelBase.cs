using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;

namespace Bot.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    [RelayCommand]
    protected static void Hide()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Hide();
        }
    }
}
