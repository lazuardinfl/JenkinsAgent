using Bot.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Agent(ILogger<Agent> logger, Config config, Jenkins jenkins, ScreenSaver screenSaver)
{
    public static readonly ManualResetEvent Mre = new(false);

    public async void Initialize()
    {
        SetEnvironmentVariable();
        bool isConfigValid = await config.Reload(true);
        if (TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) == null)
        {
            TaskSchedulerHelper.Create(config.Server.TaskSchedulerName, App.Title, App.BaseDir, true);
        }
        Mre.Set();
        await Task.Run(App.Mre.WaitOne);
        bool atStartup = (App.Lifetime().Args ?? []).Contains("startup");
        if (!(isConfigValid && await jenkins.ReloadConnection(atStartup))) { logger.LogError("Initialize failed"); }
        screenSaver.Initialize();
    }

    private static void SetEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("BotAgent", App.Title);
        Environment.SetEnvironmentVariable("BotAgent", App.Title, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(App.Title, App.ProfileDir.Replace("/", @"\"));
        Environment.SetEnvironmentVariable(App.Title, App.ProfileDir.Replace("/", @"\"), EnvironmentVariableTarget.User);
    }
}