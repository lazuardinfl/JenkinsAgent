using System;
using System.Linq;
using System.Threading;

namespace Bot.Services;

public class Agent(Config config, Jenkins jenkins, AutoStartup autoStartup, ScreenSaver screenSaver)
{
    public static readonly ManualResetEvent Mre = new(false);

    public async void Initialize()
    {
        SetEnvironmentVariable();
        if (await config.Reload())
        {
            autoStartup.Initialize();
            screenSaver.Initialize();
            await jenkins.Connect((App.Lifetime().Args ?? []).Contains("startup"));
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