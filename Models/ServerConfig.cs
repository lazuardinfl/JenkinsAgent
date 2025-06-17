using System.Text.Json.Serialization;

namespace Bot.Models;

public sealed class ServerConfig
{
    public string? AgentUrl { get; set; }
    public string? AgentPath { get; set; }
    public string? AgentVersion { get; set; }
    public string? AgentArguments { get; set; }
    public string? JavaUrl { get; set; }
    public string? JavaPath { get; set; }
    public string? JavaVersion { get; set; }
    public int ConnectTimeout { get; set; }
    public int StartupConnectTimeout { get; set; }
    public int TaskSchedulerDelay { get; set; }
    public string? TaskSchedulerName { get; set; }
    public string? ExtensionAuthUrl { get; set; }
    public string? ExtensionAuthId { get; set; }
    public string? ExtensionAuthSecret { get; set; }
    public int ScreenSaverTimeout { get; set; }
    public int ScreenSaverTimerInterval { get; set; }

    public ServerConfig()
    {
        ConnectTimeout = 10000;
        StartupConnectTimeout = 120000;
        TaskSchedulerDelay = 60;
        ScreenSaverTimeout = 600;
        ScreenSaverTimerInterval = 50000;
    }

    [JsonConstructor]
    public ServerConfig(string? agentUrl, string? agentPath, string? agentVersion, string? javaUrl, string? javaPath, string? javaVersion,
                        string? agentArguments, string? taskSchedulerName, string? extensionAuthUrl, string? extensionAuthId, string? extensionAuthSecret,
                        int connectTimeout = 10000, int startupConnectTimeout = 120000, int taskSchedulerDelay = 60, int screenSaverTimeout = 600, int screenSaverTimerInterval = 50000)
    {
        (AgentUrl, AgentPath, AgentVersion, AgentArguments, JavaUrl, JavaPath, JavaVersion) = (agentUrl, agentPath, agentVersion, agentArguments, javaUrl, javaPath, javaVersion);
        (TaskSchedulerName, ExtensionAuthUrl, ExtensionAuthId, ExtensionAuthSecret) = (taskSchedulerName?.Replace("/", @"\"), extensionAuthUrl, extensionAuthId, extensionAuthSecret);
        (ConnectTimeout, StartupConnectTimeout, TaskSchedulerDelay, ScreenSaverTimeout, ScreenSaverTimerInterval) = (connectTimeout, startupConnectTimeout, taskSchedulerDelay, screenSaverTimeout, screenSaverTimerInterval);
    }
}
