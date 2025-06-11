using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace Bot.ViewModels;

public enum Page { Config, About }

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Dictionary<Page, PageViewModelBase> pages;

    [ObservableProperty]
    private PageViewModelBase currentPage;

    [ObservableProperty]
    private string name;

    public MainWindowViewModel(Config config)
    {
        name = App.Description;
        pages = new()
        {
            { Page.Config, new ConfigViewModel(config) },
            { Page.About, new AboutViewModel() }
        };
        currentPage = pages[Page.Config];
    }

    public void Initialize() => ((ConfigViewModel)pages[Page.Config]).Initialize();

    public void Show(Page page)
    {
        CurrentPage = pages[page];
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is Window window)
            {
                window.WindowState = WindowState.Normal;
                window.Show();
                window.BringIntoView();
                window.Focus();
            }
        }
    }
}
