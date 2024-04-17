using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;

namespace Bot.ViewModels;

public abstract partial class PageViewModelBase : ViewModelBase
{
    [RelayCommand]
    protected void Hide()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            app.MainWindow!.Hide();
        }
    }
}