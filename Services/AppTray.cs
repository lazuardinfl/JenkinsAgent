using Bot.Helpers;
using Bot.Models;
using Bot.ViewModels;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

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
    private Action<Page> showMainWindow = null!;

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
        botMenuItem = new("Bot Config", (_, _) => showMainWindow.Invoke(Page.Config));
        reloadMenuItem = new("Reload", (_, _) => Reload());
        resetMenuItem = new("Reset", (_, _) => Reset());
        configSubMenu = new("Configuration") {
            Items = { botMenuItem, reloadMenuItem, resetMenuItem }
        };
        aboutMenuItem = new("About", (_, _) => showMainWindow.Invoke(Page.About));
        exitMenuItem = new("Exit", (_, _) => Exit());
        menuOrder = [
            testMenuItem, startupMenuItem, screensaverSubMenu, 
            connectionSubMenu, configSubMenu, aboutMenuItem, exitMenuItem
        ];
        config.Changed += OnConfigChanged;
        jenkins.ConnectionChanged += OnConnectionChanged;
        screenSaver.PreventLockStatusChanged += OnPreventLockStatusChanged;
        tray = CreateSystemTray(true);
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        HideMenu(testMenuItem);
        startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
        preventlockMenuItem.Enabled = false;
        HideMenu(screensaverSubMenu);
        preventlockMenuItem.Checked = config.Client.IsPreventLock;
        reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
        tray.Show();
    }

    public void RegisterShowMainWindow(MainWindowViewModel mainWindow) => showMainWindow = mainWindow.Show;

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
        logger.LogInformation("Test on thread {threadId}", Environment.CurrentManagedThreadId);
        try
        {
            //string entropy = DataProtectionHelper.Base64Encode(config.Client.BotId!)!;
            //logger.LogInformation("{data}", config.Client.BotId);
            //logger.LogInformation("{data}", entropy);
            //string encrypted = DataProtectionHelper.EncryptDataAsText(config.Client.BotId, entropy)!;
            //logger.LogInformation("{data}", encrypted);
            //string decrypted = DataProtectionHelper.DecryptDataAsText(encrypted, entropy)!;
            //logger.LogInformation("{data}", decrypted);

        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    private async void AutoStartup()
    {
        string msg = $"Are you sure to {(startupMenuItem.Checked ? "disable" : "enable")} auto startup?";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Startup", msg))
        {
            if (TaskSchedulerHelper.Enable(config.Server.TaskSchedulerName, !startupMenuItem.Checked))
            {
                startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
            }
            else
            {
                MessageBoxHelper.ShowErrorFireForget("You need to run program as admin\nand make sure bot config is valid!");
            }
        }
    }

    private async void PreventLock()
    {
        string msg = $"Are you sure to {(config.Client.IsPreventLock ? "disable" : "enable")} prevent lock?";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Prevent Lock", msg))
        {
            config.Client.IsPreventLock = !config.Client.IsPreventLock;
            preventlockMenuItem.Checked = config.Client.IsPreventLock;
            screenSaver.ReloadPreventLock();
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
                break;
            case ExtensionStatus.Invalid:
                HideMenu(screensaverSubMenu);
                preventlockMenuItem.Enabled = false;
                break;
            case ExtensionStatus.Expired:
                ShowMenu(screensaverSubMenu);
                preventlockMenuItem.Enabled = false;
                break;
        }
        expiredMenuItem.Text = $"Expired: {e.PreventLockExpiredDate:d MMMM yyyy}";
    }

    private async void Connect()
    {
        string msg;
        switch (jenkins.Status)
        {
            case ConnectionStatus.Connected:
                msg = "Are you sure to disconnect from the server?";
                if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Disconnect", msg)) {
                    jenkins.Disconnect(); 
                }
                break;
            case ConnectionStatus.Disconnected:
                msg = "Are you sure to connect to the server?";
                if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Connect", msg)) {
                    await jenkins.ReloadConnection(true);
                }
                break;
        }
    }

    private async void AutoReconnect()
    {
        string msg = $"Are you sure to {(config.Client.IsAutoReconnect ? "disable" : "enable")} auto reconnect?";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Reconnect", msg))
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
        string msg = "Are you sure to reload config?\nConnection will be reset";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Reload", msg))
        {
            await config.Reload();
            config.RaiseChanged(this, EventArgs.Empty);
        }
    }

    private async void Reset()
    {
        string msg = "Are you sure to reset config?\nYour current config will be deleted";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Reset", msg))
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
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Exit", "Are you sure to exit application?"))
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