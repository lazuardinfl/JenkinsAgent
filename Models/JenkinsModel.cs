using System;
using System.Text.Json.Serialization;

namespace Bot.Models;

public enum BotIcon { Normal, Offline }
public enum ConnectionStatus { Initialize, Connected, Disconnected }

public class JenkinsEventArgs : EventArgs
{
    public ConnectionStatus Status { get; set; }
    public BotIcon Icon { get; set; }
    public bool IsAutoReconnect { get; set; }
}

public sealed class JenkinsCredential
{
    [JsonPropertyName("OrchestratorUrl")]
    public string? Url { get; set; }
    [JsonPropertyName("BotId")]
    public string? Id { get; set; }
    [JsonPropertyName("BotToken")]
    public string? Token { get; set; }
}

public sealed class JenkinsConfig
{
    public string? AgentUrl { get; set; }
    public string? AgentPath { get; set; }
    public string? AgentVersion { get; set; }
    public string? JavaUrl { get; set; }
    public string? JavaPath { get; set; }
    public string? JavaVersion { get; set; }
    public string? JnlpUrl { get; set; }
    public int? ConnectTimeout { get; set; }
}