using Bot.Helpers;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Agent(ILogger<Agent> logger, Config config, Jenkins jenkins, ScreenSaver screenSaver)
{
    public static readonly ManualResetEvent Mre = new(false);

    public async void Initialize()
    {
        config.Changed += jenkins.OnConfigChanged;
        config.Changed += screenSaver.OnConfigChanged;
        bool isConfigValid = await config.Reload(true);
        if (TaskSchedulerHelper.GetStatus(config.Server.TaskSchedulerName) == null)
        {
            TaskSchedulerHelper.Create(config.Server.TaskSchedulerName, App.Title, App.BaseDir, true);
        }
        Mre.Set();
        await Task.Run(App.Mre.WaitOne);
        if (!(isConfigValid && await jenkins.ReloadConnection(true))) { logger.LogError("Initialize failed"); }
        screenSaver.Initialize();
    }
}