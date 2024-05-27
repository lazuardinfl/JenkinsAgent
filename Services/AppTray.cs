using Bot.Helpers;
using Bot.Models;
using Bot.ViewModels;
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
    private readonly ToolStripMenuItem testMenuItem;
    private readonly ToolStripMenuItem startupMenuItem;
    private readonly ToolStripMenuItem screensaverSubMenu, preventlockMenuItem, expiredMenuItem;
    private readonly ToolStripMenuItem connectionSubMenu, reconnectMenuItem, connectMenuItem;
    private readonly ToolStripMenuItem configSubMenu, botMenuItem, reloadMenuItem, resetMenuItem;
    private readonly ToolStripMenuItem aboutMenuItem;
    private readonly ToolStripMenuItem exitMenuItem;
    private readonly ContextMenuStrip contextMenu;
    private readonly NotifyIcon tray;
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
        testMenuItem = new("Test", null, (_, _) => Test());
        startupMenuItem = new("Auto Startup", null, (_, _) => AutoStartup());
        preventlockMenuItem = new("Prevent Screen Locked", null, (_, _) => PreventLock());
        expiredMenuItem = new("Expired");
        screensaverSubMenu = new("Screen Saver", null, [preventlockMenuItem, expiredMenuItem]);
        reconnectMenuItem = new("Auto Reconnect", null, (_, _) => AutoReconnect());
        connectMenuItem = new("Connect", null, (_, _) => Connect());
        connectionSubMenu = new("Connection", null, [reconnectMenuItem, connectMenuItem]);
        botMenuItem = new("Bot Config", null, (_, _) => showMainWindow.Invoke(Page.Config));
        reloadMenuItem = new("Reload", null, (_, _) => Reload());
        resetMenuItem = new("Reset", null, (_, _) => Reset());
        configSubMenu = new("Configuration", null, [botMenuItem, reloadMenuItem, resetMenuItem]);
        aboutMenuItem = new("About", null, (_, _) => showMainWindow.Invoke(Page.About));
        exitMenuItem = new("Exit", null, (_, _) => Exit());
        contextMenu = new()
        {
            Items = {
                testMenuItem, startupMenuItem, screensaverSubMenu,
                connectionSubMenu, configSubMenu, aboutMenuItem, exitMenuItem
            }
        };
        tray = new()
        {
            Text = "Please wait ...",
            Icon = new(icons[BotIcon.Offline]),
            Visible = false,
            ContextMenuStrip = contextMenu
        };
        config.Reloaded += OnConfigReloaded;
        jenkins.ConnectionChanged += OnConnectionChanged;
        screenSaver.PreventLockStatusChanged += OnPreventLockStatusChanged;
    }

    public async void Initialize()
    {
        contextMenu.Enabled = false;
        //testMenuItem.Available = false;
        startupMenuItem.Available = false;
        preventlockMenuItem.Enabled = false;
        screensaverSubMenu.Available = false;
        tray.Visible = true;
        await Task.Run(() => {
            App.Mre.WaitOne();
            Agent.Mre.WaitOne();
        });
        tray.Text = CreateDescription();
        preventlockMenuItem.Checked = config.Client.IsPreventLock;
        reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
        contextMenu.Enabled = true;
    }

    public void RegisterShowMainWindow(MainWindowViewModel mainWindow) => showMainWindow = mainWindow.Show;

    private async void Test()
    {
        await Task.Delay(10);
        logger.LogInformation("Test on thread {threadId}", Environment.CurrentManagedThreadId);
        try
        {
            //logger.LogInformation("{data}", App.Hash);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    private async void AutoStartup()
    {
        contextMenu.Enabled = false;
        string msg = $"Are you sure to {(startupMenuItem.Checked ? "disable" : "enable")} auto startup?";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Startup", msg))
        {
            if (TaskSchedulerHelper.Enable(config.Server.TaskSchedulerName, !startupMenuItem.Checked))
            {
                startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
            }
            else
            {
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.AdminRequired));
            }
        }
        contextMenu.Enabled = true;
    }

    private async void PreventLock()
    {
        contextMenu.Enabled = false;
        string msg = $"Are you sure to {(config.Client.IsPreventLock ? "disable" : "enable")} prevent lock?";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Prevent Lock", msg))
        {
            config.Client.IsPreventLock = !config.Client.IsPreventLock;
            preventlockMenuItem.Checked = config.Client.IsPreventLock;
            screenSaver.ReloadPreventLock();
            await config.Save();
        }
        contextMenu.Enabled = true;
    }

    private void OnPreventLockStatusChanged(object? sender, ScreenSaverEventArgs e)
    {
        App.GetUIThread().Post(() => {
            switch (e.PreventLockStatus)
            {
                case ExtensionStatus.Valid:
                    screensaverSubMenu.Available = true;
                    preventlockMenuItem.Enabled = true;
                    break;
                case ExtensionStatus.Invalid:
                    screensaverSubMenu.Available = false;
                    preventlockMenuItem.Enabled = false;
                    break;
                case ExtensionStatus.Expired:
                    screensaverSubMenu.Available = true;
                    preventlockMenuItem.Enabled = false;
                    break;
            }
            expiredMenuItem.Text = $"Expired: {e.PreventLockExpiredDate:d MMMM yyyy}";
        });
    }

    private async void Connect()
    {
        contextMenu.Enabled = false;
        string msg;
        switch (jenkins.Status)
        {
            case ConnectionStatus.Connected or ConnectionStatus.Retry:
                msg = "Are you sure to disconnect from the server?";
                if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Disconnect", msg)) {
                    jenkins.Disconnect();
                }
                break;
            case ConnectionStatus.Disconnected:
                msg = "Are you sure to connect to the server?";
                if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Connect", msg)) {
                    await jenkins.Connect();
                }
                break;
        }
        contextMenu.Enabled = true;
    }

    private async void AutoReconnect()
    {
        contextMenu.Enabled = false;
        string msg = $"Are you sure to {(config.Client.IsAutoReconnect ? "disable" : "enable")} auto reconnect?";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Auto Reconnect", msg))
        {
            switch (jenkins.Status, config.Client.IsAutoReconnect)
            {
                case (ConnectionStatus.Retry, true):
                    config.Client.IsAutoReconnect = !config.Client.IsAutoReconnect;
                    jenkins.Disconnect();
                    break;
                case (ConnectionStatus.Disconnected, false):
                    config.Client.IsAutoReconnect = !config.Client.IsAutoReconnect;
                    await jenkins.Connect();
                    break;
                default:
                    config.Client.IsAutoReconnect = !config.Client.IsAutoReconnect;
                    break;
            }
            reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
            connectMenuItem.Enabled = !reconnectMenuItem.Checked;
            await config.Save();
        }
        contextMenu.Enabled = true;
    }

    private void OnConnectionChanged(object? sender, JenkinsEventArgs e)
    {
        App.GetUIThread().Post(() => {
            try
            {
                switch (e.Status)
                {
                    case ConnectionStatus.Initialize or ConnectionStatus.Interrupted:
                        configSubMenu.Available = false;
                        connectionSubMenu.Available = false;
                        break;
                    case ConnectionStatus.Connected or ConnectionStatus.Retry or ConnectionStatus.Disconnected:
                        configSubMenu.Available = true;
                        connectionSubMenu.Available = true;
                        connectMenuItem.Text = e.Status == ConnectionStatus.Disconnected ? "Connect" : "Disconnect";
                        break;
                }
                tray.Text = CreateDescription();
                tray.Icon = new(icons[e.Icon]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{msg}", ex.Message);
            }
        });
    }

    private async void Reload()
    {
        contextMenu.Enabled = false;
        string msg = "Are you sure to reload config?\nConnection will be reset";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Reload", msg))
        {
            await config.Reload(true);
        }
        contextMenu.Enabled = true;
    }

    private async void Reset()
    {
        contextMenu.Enabled = false;
        string msg = "Are you sure to reset config?\nYour current config will be deleted";
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Reset", msg))
        {
            await config.Reset();
            reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        }
        contextMenu.Enabled = true;
    }

    private void OnConfigReloaded(object? sender, EventArgs e)
    {
        App.GetUIThread().Post(() => {
            tray.Text = CreateDescription();
            TaskSchedulerHelper.Create(config.Server.TaskSchedulerName, App.Title, App.BaseDir, true);
            startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
        });
    }

    private async void Exit()
    {
        contextMenu.Enabled = false;
        if (DialogResult.OK == await MessageBoxHelper.ShowQuestionOkCancelAsync("Exit", "Are you sure to exit application?"))
        {
            config.Reloaded -= OnConfigReloaded;
            jenkins.ConnectionChanged -= OnConnectionChanged;
            screenSaver.PreventLockStatusChanged -= OnPreventLockStatusChanged;
            jenkins.Disconnect();
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
            await App.Exit();
            Environment.Exit(0);
        }
        contextMenu.Enabled = true;
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
        return $"{App.Description} v{App.Version?.Major}.{App.Version?.Minor}.{App.Version?.Build} " +
               $"({(App.IsAdministrator ? "Admin" : "Standard")})\nBot Id: {config.Client.BotId}\nStatus: {status}";
    }
}