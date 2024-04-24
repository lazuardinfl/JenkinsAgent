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
        OrchestratorUrl = orchestratorUrl;
        BotId = botId;
        BotToken = botToken;
        SettingsUrl = settingsUrl;
        IsAutoReconnect = isAutoReconnect;
        IsPreventLock = isPreventLock;
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
    public int? ConnectTimeout { get; set; }
    public string? TaskSchedulerName { get; set; }
    public string? ExtensionAuthUrl { get; set; }
    public string? ExtensionAuthId { get; set; }
    public string? ExtensionAuthSecret { get; set; }
    public int? ScreenSaverTimeout { get; set; }
    public double? ScreenSaverTimerInterval { get; set; }
}