using System.Text.Json.Serialization;

namespace Bot.Models;

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