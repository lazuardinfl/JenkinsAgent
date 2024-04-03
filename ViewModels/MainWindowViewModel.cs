using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Bot.Models;
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

    public MainWindowViewModel(Jenkins jenkins)
    {
        pages = new()
        {
            { Page.Config, new ConfigViewModel(jenkins) },
            { Page.About, new AboutViewModel() }
        };
        currentPage = pages[Page.Config];
        //jenkins.ConnectionChanged += OnConnectionChanged;
    }

    private async void OnConnectionChanged(object? sender, JenkinsEventArgs e)
    {
        if ((e.Status == ConnectionStatus.Disconnected) && (e.IsAutoReconnect == false))
        {
            await MessageBox.InvalidJenkinsCredential().ShowAsync();
        }
    }

    public void Show(Page page)
    {
        CurrentPage = pages[page];
        App.RunOnUIThread(() => {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
            {
                app.MainWindow!.WindowState = WindowState.Normal;
                app.MainWindow.Show();
                app.MainWindow.BringIntoView();
                app.MainWindow.Focus();
            }
        });
    }
}
