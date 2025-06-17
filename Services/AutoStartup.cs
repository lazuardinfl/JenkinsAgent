using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services;

public class AutoStartup
{
    private readonly ILogger logger;
    private readonly Config config;
    private bool isEnabled, isElevated;

    public AutoStartup(ILogger<AutoStartup> logger, Config config)
    {
        this.logger = logger;
        this.config = config;
        isEnabled = isElevated = false;
        config.Reloaded += OnConfigReloaded;
    }

    public event EventHandler? Changed;

    public bool IsEnabled
    {
        get => isEnabled;
        private set
        {
            if (isEnabled != value)
            {
                isEnabled = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsElevated
    {
        get => isElevated;
        private set
        {
            if (isElevated != value)
            {
                isElevated = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public async void Initialize()
    {
        IsEnabled = IsElevated = false;
        using (Microsoft.Win32.TaskScheduler.Task task = TaskService.Instance.GetTask(config.Server.TaskSchedulerName))
        {
            if (task is not null)
            {
                IsElevated = task.Definition.Principal.RunLevel == TaskRunLevel.Highest;
                IsEnabled = task.Enabled && (IsTaskSchedulerValid() || await CreateTaskScheduler(true, IsElevated));
            }
        }
    }

    public async Task<bool> Enable(bool enable = true)
    {
        bool result = !IsTaskSchedulerValid() ? await CreateTaskScheduler(enable, IsElevated) :
            await RunSchtasks($"/change /tn \"{config.Server.TaskSchedulerName}\" /{(enable ? "ENABLE" : "DISABLE")}");
        IsEnabled = result ? enable : IsEnabled;
        return result;
    }

    public async Task<bool> Elevate(bool elevate = true)
    {
        bool result = !IsTaskSchedulerValid() ? await CreateTaskScheduler(IsEnabled, elevate) :
            await RunSchtasks($"/change /tn \"{config.Server.TaskSchedulerName}\" /rl {(elevate ? "HIGHEST" : "LIMITED")}");
        IsElevated = result ? elevate : IsElevated;
        return result;
    }

    private bool IsTaskSchedulerValid()
    {
        try
        {
            using (TaskService ts = TaskService.Instance)
            using (TaskDefinition currentDefinition = ts.GetTask(config.Server.TaskSchedulerName).Definition,
                                  targetDefinition = CreateTaskDefinition(ts.NewTask()))
            {
                static object[] defs(TaskDefinition df) => [
                    ((ExecAction)df.Actions[0]).Path, ((ExecAction)df.Actions[0]).Arguments, ((ExecAction)df.Actions[0]).WorkingDirectory,
                    df.Triggers[0].TriggerType, df.Triggers[0].Enabled, ((LogonTrigger)df.Triggers[0]).UserId, ((LogonTrigger)df.Triggers[0]).Delay,
                    df.Principal.GroupId, df.Settings.DisallowStartIfOnBatteries, df.Settings.StopIfGoingOnBatteries
                ];
                return defs(currentDefinition).SequenceEqual(defs(targetDefinition));

            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private TaskDefinition CreateTaskDefinition(TaskDefinition definition, bool isEnabled = true, bool isElevated = false)
    {
        definition.Actions.Add("cmd", $"/c if exist \"{App.Title}.exe\" start \"\" \"{App.Title}.exe\" startup", App.BaseDir.Replace("/", @"\"));
        definition.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromSeconds(config.Server.TaskSchedulerDelay) });
        definition.Principal.GroupId = "Users";
        if (isElevated) { definition.Principal.RunLevel = TaskRunLevel.Highest; }
        definition.Settings.DisallowStartIfOnBatteries = false;
        definition.Settings.StopIfGoingOnBatteries = false;
        definition.Settings.Enabled = isEnabled;
        return definition;
    }

    private async Task<bool> CreateTaskScheduler(bool isEnabled = true, bool isElevated = false)
    {
        string file = $"{App.ProfileDir}/task.xml".Replace("/", @"\");
        using (TaskDefinition definition = CreateTaskDefinition(TaskService.Instance.NewTask(), isEnabled, isElevated))
        {
            File.WriteAllText(file, definition.XmlText);
            bool result = await RunSchtasks($"/create /tn \"{config.Server.TaskSchedulerName ?? " "}\" /xml \"{file}\" /f");
            File.Delete(file);
            return result;
        }
    }

    private async Task<bool> RunSchtasks(string args)
    {
        try
        {
            using (Process process = new())
            {
                process.StartInfo.FileName = "schtasks";
                process.StartInfo.Arguments = args;
                process.StartInfo.Verb = "runas";
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
                await process.WaitForExitAsync();
                if (process.ExitCode is not 0)
                {
                    throw new InvalidOperationException("Error while executing schtasks");
                }
            }
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private void OnConfigReloaded(object? sender, EventArgs e) => Initialize();
}
