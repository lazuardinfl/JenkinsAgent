using System.Text.Json.Serialization;

namespace Bot.Models;

public enum BotIcon { Normal, Offline }
public enum ConnectionStatus { Initialize, Connected, Disconnected }
public enum ExtensionStatus { Valid, Invalid, Expired }

public class Config
{
    public ClientConfig Client { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
}

public sealed class ClientConfig
{
    public string? SettingsUrl { get; set; }
    public string? OrchestratorUrl { get; set; }
    public string? BotId { get; set; }
    public string? BotToken { get; set; }
    [JsonPropertyName("AutoReconnect")]
    public bool IsAutoReconnect { get; set; }
    [JsonPropertyName("AutoStartup")]
    public bool IsAutoStartup { get; set; }
    [JsonPropertyName("PreventLock")]
    public bool IsPreventLock { get; set; }

    public ClientConfig()
    {
    }

    [JsonConstructor]
    public ClientConfig(string? orchestratorUrl, string? botId, string? botToken, bool isPreventLock,
                       string settingsUrl = "public/config/bot.json", bool isAutoReconnect = true, bool isAutoStartup = true)
    {
        SettingsUrl = settingsUrl;
        OrchestratorUrl = orchestratorUrl;
        BotId = botId;
        BotToken = botToken;
        IsAutoReconnect = isAutoReconnect;
        IsAutoStartup = isAutoStartup;
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
    public string? ExtensionAuthUrl { get; set; }
    public string? ExtensionAuthId { get; set; }
    public string? ExtensionAuthSecret { get; set; }
    public int? ScreenSaverTimeout { get; set; }
    public double? ScreenSaverTimerInterval { get; set; }
}