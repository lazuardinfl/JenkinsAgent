using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Bot.ViewModels;

public enum Page { Config, About }

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger logger;

    private readonly Dictionary<Page, PageViewModelBase> pages;

    [ObservableProperty]
    private PageViewModelBase currentPage;

    [ObservableProperty]
    private string name;

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, Config config)
    {
        this.logger = logger;
        name = App.Description;
        pages = new()
        {
            { Page.Config, new ConfigViewModel(config) },
            { Page.About, new AboutViewModel() }
        };
        currentPage = pages[Page.Config];
    }

    public void Initialize()
    {
        ((ConfigViewModel)pages[Page.Config]).Initialize();
        logger.LogInformation("Application main window initialized");
    }

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

    public void Close()
    {
        if (CurrentPage is ConfigViewModel configView)
        {
            configView.SetValueOnUI();
        }
    }
}
