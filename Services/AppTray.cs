using Bot.Models;
using Bot.ViewModels;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services;

public class AppTray
{
    private readonly ILogger logger;
    private readonly Config config;
    private readonly Agent agent;
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

    public AppTray(ILogger<AppTray> logger, Config config, Agent agent, Jenkins jenkins, ScreenSaver screenSaver)
    {
        this.logger = logger;
        this.config = config;
        this.agent = agent;
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
        config.Client.IsPreventLock = !config.Client.IsPreventLock;
        preventlockMenuItem.Checked = config.Client.IsPreventLock;
        screenSaver.SetPreventLock();
        await agent.SaveConfig();
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
        switch (jenkins.Status)
        {
            case ConnectionStatus.Connected:
                jenkins.Disonnect();
                break;
            case ConnectionStatus.Disconnected:
                if (!(await jenkins.Initialize() && await jenkins.Connect(true)))
                {
                    App.RunOnUIThread(async () => {
                        await MessageBox.Error("Connection failed. Make sure connected\n" +
                                               "to server and bot config is valid!").ShowAsync();
                    });
                }
                break;
        }
    }

    private void AutoReconnect()
    {
        config.Client.IsAutoReconnect = !config.Client.IsAutoReconnect;
        reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
    }

    private void Reload()
    {
        logger.LogInformation("Reload clicked");
    }

    private void Reset()
    {
        logger.LogInformation("Reset clicked");
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

    private void Exit()
    {
        jenkins.Disonnect();
        tray.Dispose();
        App.Exit();
        Environment.Exit(0);
    }

    private nint GetIcon(BotIcon icon) => new System.Drawing.Icon(icons[icon]).Handle;

    public void RegisterMainWindow(MainWindowViewModel mw) => mainWindow = mw;
}