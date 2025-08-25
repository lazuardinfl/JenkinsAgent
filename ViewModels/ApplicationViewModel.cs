using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Bot.Helpers;
using Bot.Models;
using Bot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.ViewModels;

public partial class ApplicationViewModel : ViewModelBase
{
    private readonly ILogger logger;
    private readonly Config config;
    private readonly Jenkins jenkins;
    private readonly AutoStartup autoStartup;
    private readonly ScreenSaver screenSaver;
    private readonly Dictionary<BotIcon, WindowIcon> icons;
    [ObservableProperty]
    private WindowIcon icon;
    [ObservableProperty]
    private string toolTipText;
    [ObservableProperty]
    private bool isVisible;

    public TrayMenu StartupSubMenu { get; }
    public TrayMenu StartupMenu { get; }
    public TrayMenu StartupElevatedMenu { get; }
    public TrayMenu ScreensaverSubMenu { get; }
    public TrayMenu PreventLockMenu { get; }
    public TrayMenu ExpiredMenu { get; }
    public TrayMenu ConnectionSubMenu { get; }
    public TrayMenu ReconnectMenu { get; }
    public TrayMenu ConnectMenu { get; }
    public TrayMenu ConfigSubMenu { get; }
    public TrayMenu ReloadMenu { get; }
    public TrayMenu ResetMenu { get; }
    public TrayMenu ExitMenu { get; }
    public Action<Page> ShowPage { get; set; }

    public ApplicationViewModel(ILogger<ApplicationViewModel> logger, Config config, Jenkins jenkins, AutoStartup autoStartup, ScreenSaver screenSaver)
    {
        this.logger = logger;
        this.config = config;
        this.jenkins = jenkins;
        this.autoStartup = autoStartup;
        this.screenSaver = screenSaver;
        icons = new() {
            { BotIcon.Normal, new(AssetLoader.Open(new Uri($"avares://{App.Title}/Assets/normal.ico"))) },
            { BotIcon.Offline, new(AssetLoader.Open(new Uri($"avares://{App.Title}/Assets/offline.ico"))) }
        };
        icon = icons[BotIcon.Offline];
        toolTipText = "Please wait ...";
        isVisible = false;
        StartupSubMenu = new("Auto Startup", false);
        StartupMenu = new("Auto Start at Logon", AutoStartup);
        StartupElevatedMenu = new("Auto Start as Admin", AutoStartupElevated);
        ScreensaverSubMenu = new("Screensaver", false);
        PreventLockMenu = new("Prevent Screen Locked", PreventLock);
        ExpiredMenu = new("Expired");
        ConnectionSubMenu = new("Connection", false);
        ReconnectMenu = new("Auto Reconnect", AutoReconnect);
        ConnectMenu = new("Connect", Connect);
        ConfigSubMenu = new("Settings", false);
        ReloadMenu = new("Reload", Reload);
        ResetMenu = new("Reset", Reset);
        ExitMenu = new("Exit", Exit);
        ShowPage = p => { };
        config.Reloaded += OnConfigReloaded;
        jenkins.ConnectionChanged += OnConnectionChanged;
        autoStartup.Changed += OnAutoStartupChanged;
        screenSaver.PreventLockStatusChanged += OnPreventLockStatusChanged;
    }

    public async void Initialize()
    {
        IsVisible = true;
        await Task.Run(Agent.Mre.WaitOne);
        ToolTipText = CreateDescription();
        PreventLockMenu.IsChecked = config.Client.IsPreventLock;
        ReconnectMenu.IsChecked = config.Client.IsAutoReconnect;
        ConnectMenu.IsEnabled = !ReconnectMenu.IsChecked;
        StartupSubMenu.IsVisible = true;
        ConfigSubMenu.IsVisible = true;
    }

    public void ShowMainWindow(Page page) => ShowPage(page);

    private async Task AutoStartup()
    {
        StartupSubMenu.IsEnabled = ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        StartupMenu.IsChecked = !StartupMenu.IsChecked;
        string msg = $"Are you sure to {(StartupMenu.IsChecked ? "disable" : "enable")} auto start at user logon?";
        if ((MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Startup", msg)) && !await autoStartup.Enable(!StartupMenu.IsChecked))
        {
            MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.UnexpectedError));
        }
        StartupSubMenu.IsEnabled = ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task AutoStartupElevated()
    {
        StartupSubMenu.IsEnabled = ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        StartupElevatedMenu.IsChecked = !StartupElevatedMenu.IsChecked;
        string msg = $"Are you sure to {(StartupElevatedMenu.IsChecked ? "disable" : "enable")} auto start with admin privileges?";
        if ((MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Startup", msg)) && !await autoStartup.Elevate(!StartupElevatedMenu.IsChecked))
        {
            MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.UnexpectedError));
        }
        StartupSubMenu.IsEnabled = ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task PreventLock()
    {
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        PreventLockMenu.IsChecked = config.Client.IsPreventLock;
        string msg = $"Are you sure to {(config.Client.IsPreventLock ? "disable" : "enable")} prevent lock?";
        if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Prevent Lock", msg))
        {
            config.Client.IsPreventLock = !config.Client.IsPreventLock;
            PreventLockMenu.IsChecked = config.Client.IsPreventLock;
            screenSaver.ReloadPreventLock();
            await config.Save();
        }
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task AutoReconnect()
    {
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        ReconnectMenu.IsChecked = config.Client.IsAutoReconnect;
        string msg = $"Are you sure to {(config.Client.IsAutoReconnect ? "disable" : "enable")} auto reconnect?";
        if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Reconnect", msg))
        {
            switch (jenkins.Status, config.Client.IsAutoReconnect)
            {
                case (ConnectionStatus.Retry, true):
                    jenkins.Disconnect();
                    break;
                case (ConnectionStatus.Disconnected, false):
                    await jenkins.Connect();
                    break;
            }
            config.Client.IsAutoReconnect = !config.Client.IsAutoReconnect;
            ReconnectMenu.IsChecked = config.Client.IsAutoReconnect;
            ConnectMenu.IsEnabled = !ReconnectMenu.IsChecked;
            await config.Save();
        }
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task Connect()
    {
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        string msg;
        switch (jenkins.Status)
        {
            case ConnectionStatus.Connected or ConnectionStatus.Retry:
                msg = "Are you sure to disconnect from the server?";
                if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Disconnect", msg))
                {
                    jenkins.Disconnect();
                }
                break;
            case ConnectionStatus.Disconnected:
                msg = "Are you sure to connect to the server?";
                if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Connect", msg))
                {
                    await jenkins.Connect();
                }
                break;
        }
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task Reload()
    {
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        string msg = "Are you sure to reload config?\nConnection will be reset";
        if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Reload", msg))
        {
            await config.Reload();
        }
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task Reset()
    {
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = false;
        string msg = "Are you sure to reset config?\nYour current config will be deleted";
        if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Reset", msg))
        {
            await config.Reset();
        }
        ConnectionSubMenu.IsEnabled = ConfigSubMenu.IsEnabled = true;
    }

    private async Task Exit()
    {
        if (MessageBoxResult.Ok == await MessageBoxHelper.ShowQuestionOkCancelAsync("Exit", "Are you sure to exit application?"))
        {
            config.Reloaded -= OnConfigReloaded;
            jenkins.ConnectionChanged -= OnConnectionChanged;
            autoStartup.Changed -= OnAutoStartupChanged;
            screenSaver.PreventLockStatusChanged -= OnPreventLockStatusChanged;
            jenkins.Disconnect();
            logger.LogInformation("Application exiting");
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            Environment.Exit(0);
        }
    }

    private void OnAutoStartupChanged(object? sender, EventArgs e)
    {
        StartupMenu.IsChecked = autoStartup.IsEnabled;
        StartupElevatedMenu.IsChecked = autoStartup.IsElevated;
        StartupSubMenu.IsVisible = true;
    }

    private void OnPreventLockStatusChanged(object? sender, EventArgs e)
    {
        switch (screenSaver.PreventLockStatus)
        {
            case ExtensionStatus.Valid or ExtensionStatus.GracePeriod:
                ScreensaverSubMenu.IsVisible = true;
                PreventLockMenu.IsEnabled = true;
                break;
            case ExtensionStatus.Invalid:
                ScreensaverSubMenu.IsVisible = false;
                PreventLockMenu.IsEnabled = false;
                break;
            case ExtensionStatus.Expired:
                ScreensaverSubMenu.IsVisible = true;
                PreventLockMenu.IsEnabled = false;
                break;
        }
        ExpiredMenu.Header = screenSaver.PreventLockStatus is ExtensionStatus.GracePeriod ?
            $"Grace Period: {screenSaver.PreventLockExpiredDate:d MMM yyyy - HH:mm}" :
            $"Expired: {screenSaver.PreventLockExpiredDate:d MMMM yyyy}";
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        switch (jenkins.Status)
        {
            case ConnectionStatus.Initialize or ConnectionStatus.Interrupted:
                ConfigSubMenu.IsVisible = false;
                ConnectionSubMenu.IsVisible = false;
                break;
            case ConnectionStatus.Connected or ConnectionStatus.Retry or ConnectionStatus.Disconnected:
                ConfigSubMenu.IsVisible = true;
                ConnectionSubMenu.IsVisible = true;
                ConnectMenu.Header = jenkins.Status == ConnectionStatus.Disconnected ? "Connect" : "Disconnect";
                break;
        }
        ToolTipText = CreateDescription();
        Icon = icons[jenkins.Status == ConnectionStatus.Connected ? BotIcon.Normal : BotIcon.Offline];
    }

    private void OnConfigReloaded(object? sender, EventArgs e)
    {
        if (!config.IsValid)
        {
            ToolTipText = CreateDescription();
            PreventLockMenu.IsChecked = config.Client.IsPreventLock;
            ReconnectMenu.IsChecked = config.Client.IsAutoReconnect;
        }
    }

    private string CreateDescription()
    {
        string status = jenkins.Status switch
        {
            ConnectionStatus.Connected => "Connected to server",
            ConnectionStatus.Disconnected => "Disconnected from server",
            ConnectionStatus.Initialize => "Initialize, please wait",
            ConnectionStatus.Retry => "Retry connection",
            ConnectionStatus.Interrupted => "Interrupted",
            ConnectionStatus.Unknown => "Unknown",
            _ => "",
        };
        return $"{App.Description} v{App.Version}{(App.IsElevated ? " (Admin)" : "")}\n" +
               $"Bot Id: {config.Client.BotId}\nStatus: {status}";
    }
}
