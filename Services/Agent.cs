using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;

namespace Bot.Services;

public class Agent(ILogger<Agent> logger, Config config, Jenkins jenkins, AutoStartup autoStartup, ScreenSaver screenSaver)
{
    public static readonly ManualResetEvent Mre = new(false);

    public async void Initialize()
    {
        SetEnvironmentVariable();
        if (await config.Reload(false))
        {
            logger.LogInformation("Application starting");
            autoStartup.Initialize();
            screenSaver.Initialize();
            await jenkins.Connect(Environment.GetCommandLineArgs().Contains("startup"));
        }
        Mre.Set();
    }

    private static void SetEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("BotAgent", App.Title);
        Environment.SetEnvironmentVariable("BotAgent", App.Title, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(App.Title, App.ProfileDir.Replace("/", @"\"));
        Environment.SetEnvironmentVariable(App.Title, App.ProfileDir.Replace("/", @"\"), EnvironmentVariableTarget.User);
    }
}
