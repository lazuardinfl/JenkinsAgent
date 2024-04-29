using Bot.Models;
using Microsoft.Extensions.Logging;
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
    private readonly ToolStripMenuItem exitMenuItem;
    private readonly ContextMenuStrip contextMenu;
    private readonly NotifyIcon tray;

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
        exitMenuItem = new("Exit", null, (_, _) => Exit());
        contextMenu = new()
        {
            DefaultDropDownDirection = ToolStripDropDownDirection.AboveRight,
            Items = {
                exitMenuItem
            }
        };
        tray = new()
        {
            Text = $"{App.Description}\n{jenkins.Status}",
            Icon = new(icons[BotIcon.Normal]),
            Visible = false,
            ContextMenuStrip = contextMenu
        };
    }

    public async void Initialize()
    {
        await Task.Run(Agent.Mre.WaitOne);
        tray.Visible = true;
    }

    private void Exit()
    {
        tray.Visible = false;
        tray.Dispose();
        Application.Exit();
    }
}