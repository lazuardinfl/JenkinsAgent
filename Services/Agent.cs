using Bot.Helpers;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Agent(Config config, Jenkins jenkins, ScreenSaver screenSaver)
{
    public static readonly ManualResetEvent Mre = new(false);

    public async void Initialize()
    {
        SetEnvironmentVariable();
        await config.Reload();
        if (TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) == null)
        {
            TaskSchedulerHelper.Create(config.Server.TaskSchedulerName, App.Title, App.BaseDir, true);
        }
        Mre.Set();
        await Task.Run(App.Mre.WaitOne);
        bool atStartup = (App.Lifetime().Args ?? []).Contains("startup");
        if (config.IsValid)
        {
            await jenkins.Connect(atStartup);
            screenSaver.Initialize();
        }
    }

    private static void SetEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("BotAgent", App.Title);
        Environment.SetEnvironmentVariable("BotAgent", App.Title, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(App.Title, App.ProfileDir.Replace("/", @"\"));
        Environment.SetEnvironmentVariable(App.Title, App.ProfileDir.Replace("/", @"\"), EnvironmentVariableTarget.User);
    }
}