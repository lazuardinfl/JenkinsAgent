using System.Text.Json.Serialization;

namespace Bot.Models;

public sealed class ClientConfig
{
    public string? OrchestratorUrl { get; set; }
    public string? SettingsUrl { get; set; } = "public/config/bot.json";
    public string? BotId { get; set; }
    public string? BotToken { get; set; }
    [JsonPropertyName("UseWindowsCertStore")]
    public bool IsWindowsCertStoreUsed { get; set; } = true;
    [JsonPropertyName("AutoReconnect")]
    public bool IsAutoReconnect { get; set; } = true;
    [JsonPropertyName("PreventLock")]
    public bool IsPreventLock { get; set; } = false;
}
