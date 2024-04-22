using Bot.Models;
using Bot.ViewModels;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services;

public class AppTray
{
    private readonly ILogger logger;
    private readonly Config config;
    private readonly Jenkins jenkins;
    private readonly ScreenSaver screenSaver;
    private readonly Dictionary<BotIcon, string> icons;
    private readonly PopupMenuItem testMenuItem;
    private readonly PopupMenuItem startupMenuItem;
    private readonly PopupMenuItem preventlockMenuItem;
    private readonly PopupMenuItem expiredMenuItem;
    private readonly PopupSubMenu screensaverSubMenu;
    private readonly PopupMenuItem reconnectMenuItem;
    private readonly PopupMenuItem connectMenuItem;
    private readonly PopupSubMenu connectionSubMenu;
    private readonly PopupMenuItem botMenuItem;
    private readonly PopupMenuItem reloadMenuItem;
    private readonly PopupMenuItem resetMenuItem;
    private readonly PopupSubMenu configSubMenu;
    private readonly PopupMenuItem aboutMenuItem;
    private readonly PopupMenuItem exitMenuItem;
    private TrayIconWithContextMenu tray;
    private MainWindowViewModel mainWindow = null!;

    public AppTray(ILogger<AppTray> logger, Config config, Jenkins jenkins, ScreenSaver screenSaver)
    {
        this.logger = logger;
        this.config = config;
        this.jenkins = jenkins;
        this.screenSaver = screenSaver;
        icons = new() {
            { BotIcon.Normal, $"{App.BaseDir}/resources/normal.ico" },
            { BotIcon.Offline, $"{App.BaseDir}/resources/offline.ico" }
        };
        testMenuItem = new("Test", (_, _) => Test()) { Visible = false };
        startupMenuItem = new("Auto Startup", (_, _) => AutoStartup());
        preventlockMenuItem = new("Prevent Screen Locked", (_, _) => PreventLock());
        expiredMenuItem = new() { Text = "Expired" };
        screensaverSubMenu = new("Screen Saver") {
            Items = { preventlockMenuItem, expiredMenuItem }
        };
        reconnectMenuItem = new("Auto Reconnect", (_, _) => AutoReconnect());
        connectMenuItem = new("Connect", (_, _) => Connect());
        connectionSubMenu = new("Connection") {
            Items = { reconnectMenuItem, connectMenuItem }
        };
        botMenuItem = new("Bot Config", (_, _) => ShowMainWindow(Page.Config));
        reloadMenuItem = new("Reload", (_, _) => Reload());
        resetMenuItem = new("Reset", (_, _) => Reset());
        configSubMenu = new("Configuration") {
            Items = { botMenuItem, reloadMenuItem, resetMenuItem }
        };
        aboutMenuItem = new("About", (_, _) => ShowMainWindow(Page.About));
        exitMenuItem = new("Exit", (_, _) => Exit());
        screenSaver.PreventLockStatusChanged += OnPreventLockStatusChanged;
        jenkins.ConnectionChanged += OnConnectionChanged;
        tray = CreateSystemTray(true);
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        startupMenuItem.Checked = config.Client.IsAutoStartup;
        preventlockMenuItem.Enabled = false;
        screensaverSubMenu.Visible = false;
        preventlockMenuItem.Checked = config.Client.IsPreventLock;
        reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
        tray.Show();
    }

    private void AutoStartup()
    {
        logger.LogInformation("Auto Startup clicked");
    }

    private void OnPreventLockStatusChanged(object? sender, ScreenSaverEventArgs e)
    {
        switch (e.PreventLockStatus)
        {
            case ExtensionStatus.Valid:
                screensaverSubMenu.Visible = true;
                preventlockMenuItem.Enabled = true;
                expiredMenuItem.Text = $"Expired: {e.PreventLockExpiredDate:d MMMM yyyy}";
                break;
            case ExtensionStatus.Invalid:
                screensaverSubMenu.Visible = false;
                preventlockMenuItem.Enabled = false;
                break;
            case ExtensionStatus.Expired:
                screensaverSubMenu.Visible = true;
                preventlockMenuItem.Enabled = false;
                expiredMenuItem.Text = $"Expired: {e.PreventLockExpiredDate:d MMMM yyyy}";
                break;
        }
    }

    private async void PreventLock()
    {
        ButtonResult result = await App.GetUIThread().InvokeAsync(async () => {
            return await MessageBox.QuestionYesNo("Prevent Lock", $"Are you sure to {(config.Client.IsPreventLock ? "disable" : "enable")} prevent lock?").ShowAsync();
        });
        if (result == ButtonResult.Yes)
        {
            config.Client.IsPreventLock = !config.Client.IsPreventLock;
            preventlockMenuItem.Checked = config.Client.IsPreventLock;
            screenSaver.SetPreventLock();
            await config.Save();
        }
    }

    private void OnConnectionChanged(object? sender, JenkinsEventArgs e)
    {
        switch (e.Status)
        {
            case ConnectionStatus.Initialize:
                configSubMenu.Visible = false;
                connectionSubMenu.Visible = false;
                tray.UpdateToolTip($"{App.Description}\nInitialize, please wait");
                break;
            case ConnectionStatus.Connected:
                configSubMenu.Visible = true;
                connectionSubMenu.Visible = true;
                connectMenuItem.Text = "Disconnect";
                tray.UpdateToolTip($"{App.Description}\n{e.Status}");
                break;
            case ConnectionStatus.Disconnected:
                configSubMenu.Visible = true;
                connectionSubMenu.Visible = true;
                connectMenuItem.Text = "Connect";
                tray.UpdateToolTip($"{App.Description}\n{e.Status}");
                break;
        }
        tray.UpdateIcon(GetIcon(e.Icon));
    }

    private async void Connect()
    {
        ButtonResult result;
        switch (jenkins.Status)
        {
            case ConnectionStatus.Connected:
                result = await App.GetUIThread().InvokeAsync(async () => {
                    return await MessageBox.QuestionYesNo("Disconnect", "Are you sure to disconnect from the server?").ShowAsync();
                });
                if (result == ButtonResult.Yes) { jenkins.Disconnect(); }
                break;
            case ConnectionStatus.Disconnected:
                result = await App.GetUIThread().InvokeAsync(async () => {
                    return await MessageBox.QuestionYesNo("Connect", "Are you sure to connect to the server?").ShowAsync();
                });
                if (result == ButtonResult.Yes) { await jenkins.ReloadConnection(true); }
                break;
        }
    }

    private async void AutoReconnect()
    {
        ButtonResult result = await App.GetUIThread().InvokeAsync(async () => {
            return await MessageBox.QuestionYesNo("Auto Reconnect", $"Are you sure to {(config.Client.IsAutoReconnect ? "disable" : "enable")} auto reconnect?").ShowAsync();
        });
        if (result == ButtonResult.Yes)
        {
            switch (jenkins.Status, config.Client.IsAutoReconnect)
            {
                case (ConnectionStatus.Disconnected, true):
                    jenkins.Disconnect(false);
                    break;
                case (ConnectionStatus.Disconnected, false):
                    await jenkins.ReloadConnection(true);
                    break;
            }
            config.Client.IsAutoReconnect = !config.Client.IsAutoReconnect;
            reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
            connectMenuItem.Enabled = !reconnectMenuItem.Checked;
            await config.Save();
        }
    }

    private async void Reload()
    {
        ButtonResult result = await App.GetUIThread().InvokeAsync(async () => {
            return await MessageBox.QuestionYesNo("Reload", $"Are you sure to reload config?\nConnection will be reset").ShowAsync();
        });
        if (result == ButtonResult.Yes)
        {
            await config.Reload();
            config.RaiseChanged(this, EventArgs.Empty);
        }
    }

    private async void Reset()
    {
        ButtonResult result = await App.GetUIThread().InvokeAsync(async () => {
            return await MessageBox.QuestionYesNo("Reset", $"Are you sure to reset config?\nYour current config will be deleted").ShowAsync();
        });
        if (result == ButtonResult.Yes)
        {
            config.Client = new();
            config.Server = new();
            await config.Save();
            config.RaiseChanged(this, EventArgs.Empty);
        }
    }

    private void Test()
    {
        logger.LogInformation("TEST");
        try
        {
            config.Client.OrchestratorUrl = config.Client.OrchestratorUrl!.Replace("http", "https");
            config.Client.OrchestratorUrl = config.Client.OrchestratorUrl.Replace("local", "test");
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    private TrayIconWithContextMenu CreateSystemTray(bool hidden = false)
    {
        TrayIconWithContextMenu trayIcon = new()
        {
            Icon = GetIcon(jenkins.Status == ConnectionStatus.Connected ? BotIcon.Normal : BotIcon.Offline),
            ToolTip = $"{App.Description}\n{jenkins.Status}",
            ContextMenu = new PopupMenu
            {
                Items = {
                    testMenuItem, startupMenuItem, screensaverSubMenu, connectionSubMenu, configSubMenu, aboutMenuItem, exitMenuItem
                }
            }
        };
        trayIcon.MessageWindow.TaskbarCreated += OnCrash;
        if (hidden) { trayIcon.Visibility = IconVisibility.Hidden; }
        trayIcon.Create();
        return trayIcon;
    }

    private async void OnCrash(object? sender, EventArgs e)
    {
        tray.Dispose();
        await Task.Delay(10000);
        tray = CreateSystemTray();
        logger.LogWarning("Tray icon reset");
    }

    private void ShowMainWindow(Page page) => mainWindow.Show(page);

    private async void Exit()
    {
        jenkins.Disconnect();
        tray.Dispose();
        await App.Exit();
        Environment.Exit(0);
    }

    private nint GetIcon(BotIcon icon) => new System.Drawing.Icon(icons[icon]).Handle;

    public void RegisterMainWindow(MainWindowViewModel mw) => mainWindow = mw;
}