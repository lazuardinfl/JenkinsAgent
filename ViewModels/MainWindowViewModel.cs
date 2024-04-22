using Avalonia.Controls;
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
    private string? name = App.Description;

    public MainWindowViewModel(Config config)
    {
        pages = new()
        {
            { Page.Config, new ConfigViewModel(config) },
            { Page.About, new AboutViewModel() }
        };
        currentPage = pages[Page.Config];
    }

    public void Show(Page page)
    {
        CurrentPage = pages[page];
        App.GetUIThread().Post(() => {
            App.Lifetime().MainWindow!.WindowState = WindowState.Normal;
            App.Lifetime().MainWindow!.Show();
            App.Lifetime().MainWindow!.BringIntoView();
            App.Lifetime().MainWindow!.Focus();
        });
    }
}