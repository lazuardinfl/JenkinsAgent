using System.Text.Json.Serialization;

namespace Bot.Models;

public sealed class ExtensionConfig
{
    [JsonPropertyName("ExtensionAuthUrl")]
    public string? AuthUrl { get; set; }
    [JsonPropertyName("ExtensionAuthId")]
    public string? AuthId { get; set; }
    [JsonPropertyName("ExtensionAuthSecret")]
    public string? AuthSecret { get; set; }
}