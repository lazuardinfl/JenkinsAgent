using Bot.Helpers;
using Bot.Models;
using Bot.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bot.Services;

public class SystemTray
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

    public SystemTray(ILogger<SystemTray> logger, Config config, Jenkins jenkins, ScreenSaver screenSaver)
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
            Text = $"{App.Description}\n{jenkins.Status}",
            Icon = new(icons[BotIcon.Normal]),
            Visible = false,
            ContextMenuStrip = contextMenu
        };
        config.Changed += OnConfigChanged;
        jenkins.ConnectionChanged += OnConnectionChanged;
        screenSaver.PreventLockStatusChanged += OnPreventLockStatusChanged;
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        testMenuItem.Visible = false;
        startupMenuItem.Checked = TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) ?? false;
        preventlockMenuItem.Enabled = false;
        screensaverSubMenu.Visible = false;
        preventlockMenuItem.Checked = config.Client.IsPreventLock;
        reconnectMenuItem.Checked = config.Client.IsAutoReconnect;
        connectMenuItem.Enabled = !reconnectMenuItem.Checked;
        tray.Visible = true;
    }

    public void RegisterShowMainWindow(MainWindowViewModel mainWindow) => showMainWindow = mainWindow.Show;

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
                screensaverSubMenu.Visible = true;
                preventlockMenuItem.Enabled = true;
                break;
            case ExtensionStatus.Invalid:
                screensaverSubMenu.Visible = false;
                preventlockMenuItem.Enabled = false;
                break;
            case ExtensionStatus.Expired:
                screensaverSubMenu.Visible = true;
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
                    await jenkins.ReloadConnection();
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
                    await jenkins.ReloadConnection();
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
                configSubMenu.Visible = false;
                connectionSubMenu.Visible = false;
                tray.Text = $"{App.Description}\nInitialize, please wait";
                break;
            case ConnectionStatus.Connected:
                configSubMenu.Visible = true;
                connectionSubMenu.Visible = true;
                connectMenuItem.Text = "Disconnect";
                tray.Text = $"{App.Description}\n{e.Status}";
                break;
            case ConnectionStatus.Disconnected:
                configSubMenu.Visible = true;
                connectionSubMenu.Visible = true;
                connectMenuItem.Text = "Connect";
                tray.Text = $"{App.Description}\n{e.Status}";
                break;
        }
        tray.Icon = new(icons[e.Icon]);
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

    private void Exit()
    {
        tray.Visible = false;
        tray.Dispose();
        Application.Exit();
    }
}