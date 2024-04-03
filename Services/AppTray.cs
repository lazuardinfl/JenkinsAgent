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
    private readonly Jenkins jenkins;
    private readonly Dictionary<BotIcon, string> icons;
    private readonly PopupMenuItem testMenuItem;
    private readonly PopupMenuItem startupMenuItem;
    private readonly PopupMenuItem reconnectMenuItem;
    private readonly PopupMenuItem connectMenuItem;
    private readonly PopupSubMenu connectionSubMenu;
    private readonly PopupMenuItem botMenuItem;
    private readonly PopupMenuItem reloadMenuItem;
    private readonly PopupSubMenu configSubMenu;
    private readonly PopupMenuItem aboutMenuItem;
    private readonly PopupMenuItem exitMenuItem;
    private TrayIconWithContextMenu tray;
    private MainWindowViewModel mainWindow = null!;

    public AppTray(ILogger<AppTray> logger, Jenkins jenkins)
    {
        this.logger = logger;
        this.jenkins = jenkins;
        icons = new() {
            { BotIcon.Normal, $"{App.BaseDir}/resources/normal.ico" },
            { BotIcon.Offline, $"{App.BaseDir}/resources/offline.ico" }
        };
        startupMenuItem = new("Auto Startup", (_, _) => Test()) { Checked = true };
        reconnectMenuItem = new("Auto Reconnect", (_, _) => AutoReconnect());
        connectMenuItem = new("Connect", (_, _) => Connect());
        connectionSubMenu = new("Connection") {
            Items = { reconnectMenuItem, connectMenuItem }
        };
        botMenuItem = new("Bot Config", (_, _) => ShowMainWindow(Page.Config));
        reloadMenuItem = new("Reload", (_, _) => Test());
        configSubMenu = new("Configuration") {
            Items = { botMenuItem, reloadMenuItem }
        };
        aboutMenuItem = new("About", (_, _) => ShowMainWindow(Page.About));
        exitMenuItem = new("Exit", (_, _) => Exit());
        testMenuItem = new("Test", (_, _) => Test());
        jenkins.ConnectionChanged += OnConnectionChanged;
        tray = CreateSystemTray(true, true);
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        reconnectMenuItem.Checked = jenkins.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
        tray.Show();
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
                        await MessageBox.InvalidJenkinsCredential().ShowAsync();
                    });
                }
                break;
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

    private void AutoReconnect()
    {
        jenkins.IsAutoReconnect = !jenkins.IsAutoReconnect;
        reconnectMenuItem.Checked = jenkins.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
    }

    private void Test()
    {
        logger.LogInformation("TEST");
        try
        {
            jenkins.Credential.Url = jenkins.Credential.Url!.Replace("http", "https");
            jenkins.Credential.Url = jenkins.Credential.Url.Replace("local", "test");
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    private TrayIconWithContextMenu CreateSystemTray(bool hidden = false, bool offline = false)
    {
        TrayIconWithContextMenu trayIcon = new()
        {
            Icon = offline ? GetIcon(BotIcon.Offline) : GetIcon(BotIcon.Normal),
            ToolTip = $"{App.Description}\n{jenkins.Status}",
            ContextMenu = new PopupMenu
            {
                Items = {
                    testMenuItem, startupMenuItem, connectionSubMenu, configSubMenu, aboutMenuItem, exitMenuItem
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
        logger.LogInformation("Tray Icon reset");
    }

    private void ShowMainWindow(Page page)
    {
        mainWindow.Show(page);
    }

    private void Exit()
    {
        jenkins.Disonnect();
        tray.Dispose();
        App.Exit();
        Environment.Exit(0);
    }

    private nint GetIcon(BotIcon icon)
    {
        return new System.Drawing.Icon(icons[icon]).Handle;
    }

    public void RegisterMainWindow(MainWindowViewModel mw) => mainWindow = mw;
}