using System.Text.Json.Serialization;

namespace Bot.Models;

public enum BotIcon { Normal, Offline }
public enum ConnectionStatus { Initialize, Connected, Disconnected }
public enum ExtensionStatus { Valid, Invalid, Expired }

public sealed class ClientConfig
{
    public string? OrchestratorUrl { get; set; }
    public string? BotId { get; set; }
    public string? BotToken { get; set; }
    public string SettingsUrl { get; set; }
    [JsonPropertyName("AutoReconnect")]
    public bool IsAutoReconnect { get; set; }
    [JsonPropertyName("PreventLock")]
    public bool IsPreventLock { get; set; }

    public ClientConfig()
    {
        SettingsUrl = App.DefaultConfigUrl;
        IsAutoReconnect = true;
    }

    [JsonConstructor]
    public ClientConfig(string? orchestratorUrl, string? botId, string? botToken, bool isPreventLock,
                       string settingsUrl = App.DefaultConfigUrl, bool isAutoReconnect = true)
    {
        (OrchestratorUrl, BotId, BotToken, SettingsUrl) = (orchestratorUrl, botId, botToken, settingsUrl);
        (IsAutoReconnect, IsPreventLock) = (isAutoReconnect, isPreventLock);
    }
}

public sealed class ServerConfig
{
    public string? AgentUrl { get; set; }
    public string? AgentPath { get; set; }
    public string? AgentVersion { get; set; }
    public string? JavaUrl { get; set; }
    public string? JavaPath { get; set; }
    public string? JavaVersion { get; set; }
    public string? JnlpUrl { get; set; }
    public int ConnectTimeout { get; set; }
    public string? TaskSchedulerName { get; set; }
    public string? ExtensionAuthUrl { get; set; }
    public string? ExtensionAuthId { get; set; }
    public string? ExtensionAuthSecret { get; set; }
    public int ScreenSaverTimeout { get; set; }
    public int ScreenSaverTimerInterval { get; set; }

    public ServerConfig()
    {
        ConnectTimeout = 10000;
        ScreenSaverTimeout = 600;
        ScreenSaverTimerInterval = 50000;
    }

    [JsonConstructor]
    public ServerConfig(string? agentUrl, string? agentPath, string? agentVersion, string? javaUrl, string? javaPath, string? javaVersion,
                        string? jnlpUrl, string? taskSchedulerName, string? extensionAuthUrl, string? extensionAuthId, string? extensionAuthSecret,
                        int connectTimeout = 10000, int screenSaverTimeout = 600, int screenSaverTimerInterval = 50000)
    {
        (AgentUrl, AgentPath, AgentVersion, JavaUrl, JavaPath, JavaVersion) = (agentUrl, agentPath, agentVersion, javaUrl, javaPath, javaVersion);
        (JnlpUrl, TaskSchedulerName, ExtensionAuthUrl, ExtensionAuthId, ExtensionAuthSecret) = (jnlpUrl, taskSchedulerName, extensionAuthUrl, extensionAuthId, extensionAuthSecret);
        ConnectTimeout = connectTimeout;
        ScreenSaverTimeout = screenSaverTimeout;
        ScreenSaverTimerInterval = screenSaverTimerInterval;
    }
}