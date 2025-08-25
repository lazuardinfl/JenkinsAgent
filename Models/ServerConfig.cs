namespace Bot.Models;

public sealed class ServerConfig
{
    public string AgentUrl { get; set; } = "jnlpJars/agent.jar";
    public string AgentPath { get; set; } = "agent.jar";
    public string AgentVersion { get; set; } = "3301.v4363ddcca_4e7";
    public string AgentArguments { get; set; } = "-url <OrchestratorUrl> -name \"<BotId>\" -secret <BotToken> -webSocket";
    public string JavaUrl { get; set; } = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.7%2B6/OpenJDK21U-jre_x64_windows_hotspot_21.0.7_6.zip";
    public string JavaPath { get; set; } = "jre/bin";
    public string JavaVersion { get; set; } = "21.0.7.0";
    public bool KillProcessTreeOnExit { get; set; } = false;
    public int ConnectTimeout { get; set; } = 10000;
    public int StartupConnectTimeout { get; set; } = 120000;
    public int TaskSchedulerDelay { get; set; } = 60;
    public string? TaskSchedulerName { get; set; }
    public string? ExtensionAuthUrl { get; set; }
    public string? ExtensionAuthId { get; set; }
    public string? ExtensionAuthSecret { get; set; }
    public int ScreenSaverTimeout { get; set; } = 600;
    public int ScreenSaverTimerInterval { get; set; } = 50000;
    public int ScreenSaverUpdateInterval { get; set; } = 180;
    public int ScreenSaverGracePeriod { get; set; } = 72;
}
