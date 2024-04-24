using Bot.Helpers;
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
    private readonly PopupSubMenu screensaverSubMenu;
    private readonly PopupMenuItem preventlockMenuItem, expiredMenuItem;
    private readonly PopupSubMenu connectionSubMenu;
    private readonly PopupMenuItem reconnectMenuItem, connectMenuItem;
    private readonly PopupSubMenu configSubMenu;
    private readonly PopupMenuItem botMenuItem, reloadMenuItem, resetMenuItem;
    private readonly PopupMenuItem aboutMenuItem;
    private readonly PopupMenuItem exitMenuItem;
    private readonly PopupItem[] menuOrder;
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
        testMenuItem = new("Test", (_, _) => Test());
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
        menuOrder = [
            testMenuItem, startupMenuItem, screensaverSubMenu, 
            connectionSubMenu, configSubMenu, aboutMenuItem, exitMenuItem
        ];
        config.Changed += OnConfigChanged;
        screenSaver.PreventLockStatusChanged += OnPreventLockStatusChanged;
        jenkins.ConnectionChanged += OnConnectionChanged;
        tray = CreateSystemTray(true);
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        //HideMenu(testMenuItem);
        startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
        preventlockMenuItem.Enabled = false;
        HideMenu(screensaverSubMenu);
        preventlockMenuItem.Checked = config.Client.IsPreventLock;
        reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
        tray.Show();
    }

    public void RegisterMainWindow(MainWindowViewModel mw) => mainWindow = mw;

    private TrayIconWithContextMenu CreateSystemTray(bool hidden = false)
    {
        TrayIconWithContextMenu trayIcon = new()
        {
            Icon = GetIcon(jenkins.Status == ConnectionStatus.Connected ? BotIcon.Normal : BotIcon.Offline),
            ToolTip = $"{App.Description}\n{jenkins.Status}",
            ContextMenu = new()
        };
        foreach (PopupItem menu in menuOrder)
        {
            trayIcon.ContextMenu.Items.Add(menu);
        }
        trayIcon.MessageWindow.TaskbarCreated += OnCrash;
        if (hidden) { trayIcon.Visibility = IconVisibility.Hidden; }
        trayIcon.Create();
        return trayIcon;
    }

    private async void Test()
    {
        await Task.Delay(10);
        logger.LogInformation("TEST");
        try
        {
            //config.Client.OrchestratorUrl = config.Client.OrchestratorUrl!.Replace("http", "https");
            //config.Client.OrchestratorUrl = config.Client.OrchestratorUrl.Replace("local", "test");
            //HideMenu(screensaverSubMenu);
            //HideMenu(configSubMenu);
            //await Task.Delay(5000);
            //ShowMenu(aboutMenuItem);
            //ShowMenu(screensaverSubMenu);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    private async void AutoStartup()
    {
        ButtonResult result = await App.GetUIThread().InvokeAsync(async () => {
            return await MessageBox.QuestionYesNo("Auto Startup", $"Are you sure to {(startupMenuItem.Checked ? "disable" : "enable")} auto startup?").ShowAsync();
        });
        if (result == ButtonResult.Yes)
        {
            if (TaskSchedulerHelper.Enable(config.Server.TaskSchedulerName, !startupMenuItem.Checked))
            {
                startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
            }
            else
            {
                App.GetUIThread().Post(async () => {
                    await MessageBox.Error("You need to run program as admin\n" +
                                           "and make sure bot config is valid!").ShowAsync();
                });   
            }
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

    private void OnPreventLockStatusChanged(object? sender, ScreenSaverEventArgs e)
    {
        switch (e.PreventLockStatus)
        {
            case ExtensionStatus.Valid:
                ShowMenu(screensaverSubMenu);
                preventlockMenuItem.Enabled = true;
                expiredMenuItem.Text = $"Expired: {e.PreventLockExpiredDate:d MMMM yyyy}";
                break;
            case ExtensionStatus.Invalid:
                HideMenu(screensaverSubMenu);
                preventlockMenuItem.Enabled = false;
                break;
            case ExtensionStatus.Expired:
                ShowMenu(screensaverSubMenu);
                preventlockMenuItem.Enabled = false;
                expiredMenuItem.Text = $"Expired: {e.PreventLockExpiredDate:d MMMM yyyy}";
                break;
        }
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

    private void OnConnectionChanged(object? sender, JenkinsEventArgs e)
    {
        switch (e.Status)
        {
            case ConnectionStatus.Initialize:
                HideMenu(configSubMenu);
                HideMenu(connectionSubMenu);
                tray.UpdateToolTip($"{App.Description}\nInitialize, please wait");
                break;
            case ConnectionStatus.Connected:
                ShowMenu(configSubMenu);
                ShowMenu(connectionSubMenu);
                connectMenuItem.Text = "Disconnect";
                tray.UpdateToolTip($"{App.Description}\n{e.Status}");
                break;
            case ConnectionStatus.Disconnected:
                ShowMenu(configSubMenu);
                ShowMenu(connectionSubMenu);
                connectMenuItem.Text = "Connect";
                tray.UpdateToolTip($"{App.Description}\n{e.Status}");
                break;
        }
        tray.UpdateIcon(GetIcon(e.Icon));
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

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        // handle task scheduler
        TaskSchedulerHelper.Create(config.Server.TaskSchedulerName, App.Title, App.BaseDir, startupMenuItem.Checked);
        startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
    }

    private void ShowMainWindow(Page page) => mainWindow.Show(page);

    private void ShowMenu(PopupItem menu)
    {
        if (!tray.ContextMenu!.Items.Contains(menu))
        {
            List<PopupItem> oldMenu = new(tray.ContextMenu.Items);
            tray.ContextMenu.Items.Clear();
            foreach (PopupItem item in menuOrder)
            {
                if (oldMenu.Contains(item) || menu.Equals(item))
                {
                    tray.ContextMenu.Items.Add(item);
                }
            }
        }
    }

    private void HideMenu(PopupItem menu)
    {
        // menu.Visible = false; fail using this to hide menu
        tray.ContextMenu!.Items.Remove(menu);
    }

    private async void Exit()
    {
        ButtonResult result = await App.GetUIThread().InvokeAsync(async () => {
            return await MessageBox.QuestionOkCancel("Exit", $"Are you sure to exit?").ShowAsync();
        });
        if (result == ButtonResult.Ok)
        {
            jenkins.Disconnect();
            tray.Dispose();
            await App.Exit();
            Environment.Exit(0);
        }
    }

    private async void OnCrash(object? sender, EventArgs e)
    {
        tray.Dispose();
        await Task.Delay(10000);
        tray = CreateSystemTray();
        logger.LogWarning("Tray icon reset");
    }

    private nint GetIcon(BotIcon icon) => new System.Drawing.Icon(icons[icon]).Handle;
}