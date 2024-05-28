using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using System;

namespace Bot.Services;

public class AutoStartup
{
    private readonly ILogger logger;
    private readonly Config config;
    private bool enabled = false;

    public AutoStartup(ILogger<AutoStartup> logger, Config config)
    {
        this.logger = logger;
        this.config = config;
        config.Reloaded += OnConfigReloaded;
    }

    public event EventHandler? Changed;

    public bool Enabled
    {
        get { return enabled; }
        private set
        {
            if (enabled != value)
            {
                enabled = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Initialize() => Enabled = GetStatus() ?? CreateTaskScheduler();

    public bool Enable(bool enabled)
    {
        try
        {
            GetTaskScheduler().Enabled = enabled;
            Enabled = enabled;
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private bool? GetStatus() => GetTaskScheduler()?.Enabled;

    private Task GetTaskScheduler() => TaskService.Instance.GetTask(config.Server.TaskSchedulerName?.Replace("/", @"\") ?? "");

    private bool CreateTaskScheduler(bool enabled = true)
    {
        try
        {
            using (TaskDefinition definition = TaskService.Instance.NewTask())
            {
                definition.Actions.Add("cmd", $"/c if exist \"{App.Title}.exe\" start \"\" \"{App.Title}.exe\" startup", App.BaseDir.Replace("/", @"\"));
                definition.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromMinutes(1) });
                definition.Principal.RunLevel = TaskRunLevel.Highest;
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.Enabled = enabled;
                TaskService.Instance.RootFolder.RegisterTaskDefinition(config.Server.TaskSchedulerName?.Replace("/", @"\") ?? " ", definition);
            }
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private void OnConfigReloaded(object? sender, EventArgs e)
        => Enabled = CreateTaskScheduler(Enabled) ? Enabled : (GetStatus() ?? false);
}