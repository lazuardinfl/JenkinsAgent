using Bot.Helpers;
using Bot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Jenkins
{
    private static readonly ManualResetEvent mre = new(false);
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Config config;
    private readonly Dictionary<ConnectionOutputEvent, string[]> outputEvents;
    private ConnectionStatus status = ConnectionStatus.Disconnected;
    private Process process = null!;

    public Jenkins(ILogger<Jenkins> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        config.Changed += OnConfigChanged;
        outputEvents = new()
        {
            { ConnectionOutputEvent.Connected, ["INFO: Connected"] },
            { ConnectionOutputEvent.DisconnectedTemporary, ["Write side closed"] },
            { ConnectionOutputEvent.DisconnectedThenRetry, ["Failed to obtain", "is not ready"] },
            { ConnectionOutputEvent.DisconnectedThenExit, [
                "buffer too short", "For input string", "Invalid byte", "takes an operand",
                "No subject alternative DNS", "SEVERE: Handshake error"
            ]}
        };
    }

    public event EventHandler<JenkinsEventArgs>? ConnectionChanged;

    public ConnectionStatus Status
    {
        get { return status; }
        private set
        {
            if (status != value)
            {
                status = value;
                JenkinsEventArgs args = new()
                {
                    Status = value,
                    Icon = value == ConnectionStatus.Connected ? BotIcon.Normal : BotIcon.Offline,
                };
                ConnectionChanged?.Invoke(this, args);
            }
        }
    }

    public async Task<bool> Initialize()
    {
        // check java
        bool isJavaReady = IsJavaVersionCompatible();
        if (!isJavaReady)
        {
            logger.LogInformation("Downloading Java");
            Status = ConnectionStatus.Initialize;
            isJavaReady = await DownloadJava();
        }
        // check agent
        bool isAgentReady = IsAgentVersionCompatible();
        if (!isAgentReady)
        {
            logger.LogInformation("Downloading Agent");
            Status = ConnectionStatus.Initialize;
            isAgentReady = await DownloadAgent();
        }
        // assign ready status
        await Task.Run(App.Mre.WaitOne);
        bool isReady = isJavaReady && isAgentReady;
        Status = isReady ? ConnectionStatus.Initialize : ConnectionStatus.Disconnected;
        return isReady;
    }

    public async Task<bool> Connect(bool atStartup = false)
    {
        try
        {
            mre.Reset();
            process = new();
            process.StartInfo.FileName = $"{App.ProfileDir}/{config.Server.JavaPath}/java.exe";
            process.StartInfo.Arguments = $"-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT -jar {config.Server.AgentPath} {CreateAgentArguments()}";
            process.StartInfo.WorkingDirectory = App.ProfileDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += new DataReceivedEventHandler(OnOutputReceived);
            process.ErrorDataReceived += new DataReceivedEventHandler(OnOutputReceived);
            process.Exited += new EventHandler(OnExited);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!await Task.Run(() => mre.WaitOne(atStartup ? config.Server.StartupConnectTimeout : config.Server.ConnectTimeout)))
            {
                Disconnect();
                MessageBoxHelper.ShowErrorFireForget("Connection failed. Make sure connected\nto server and bot config is valid!");
            }
        }
        catch (Exception e)
        {
            Status = ConnectionStatus.Disconnected;
            logger.LogError(e, "{msg}", e.Message);
            MessageBoxHelper.ShowErrorFireForget("Unexpected error. Contact admin for help!");
        }
        return Status == ConnectionStatus.Connected;
    }

    public void Disconnect()
    {
        try
        {
            process.Kill(true);
            logger.LogInformation("Jenkins disconnected");
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
        Status = ConnectionStatus.Disconnected;
    }

    public async Task<bool> ReloadConnection(bool atStartup = false)
    {
        if (!(await Initialize() && await Connect(atStartup)))
        {
            MessageBoxHelper.ShowErrorFireForget("Connection failed. Make sure connected\nto server and bot config is valid!");
            return false;
        }
        return true;
    }

    private bool IsJavaVersionCompatible()
    {
        try
        {
            string? localJavaVersion = FileVersionInfo.GetVersionInfo($"{App.ProfileDir}/{config.Server.JavaPath}/java.exe").FileVersion;
            return localJavaVersion == config.Server.JavaVersion;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private async Task<bool> DownloadJava()
    {
        string javaDir = $"{App.ProfileDir}/{Path.GetDirectoryName(config.Server.JavaPath)}";
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                using (HttpResponseMessage response = await httpClient.GetAsync(Helper.CreateUrl(config.Client.OrchestratorUrl, config.Server.JavaUrl)))
                {
                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (ZipArchive archive = new(stream))
                        {
                            if (Directory.Exists(javaDir)) { Directory.Delete(javaDir, true); }
                            archive.ExtractToDirectory(App.ProfileDir, true);
                        }
                    }
                }
            }
            DirectoryInfo javaTemp = new DirectoryInfo(App.ProfileDir).GetDirectories().OrderByDescending(d => d.LastWriteTimeUtc).First();
            Directory.Move(javaTemp.FullName, javaDir);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private bool IsAgentVersionCompatible()
    {
        try
        {
            using (Process process = new())
            {
                process.StartInfo.FileName = $"{App.ProfileDir}/{config.Server.JavaPath}/java.exe";
                process.StartInfo.Arguments = $"-jar {config.Server.AgentPath} -version";
                process.StartInfo.WorkingDirectory = App.ProfileDir;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                string localAgentVersion = process.StandardOutput.ReadToEnd();
                return localAgentVersion.Contains(config.Server.AgentVersion!);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private async Task<bool> DownloadAgent()
    {
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                using (HttpResponseMessage response = await httpClient.GetAsync(Helper.CreateUrl(config.Client.OrchestratorUrl, config.Server.AgentUrl)))
                {
                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream file = File.Create($"{App.ProfileDir}/{config.Server.AgentPath}"))
                        {
                            stream.CopyTo(file);
                        }
                    }
                }
            }
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private string CreateAgentArguments()
    {
        string arguments = config.Server.AgentArguments ?? "-secret";
        MatchCollection matches = Helper.AngleBracketsRegex().Matches(arguments);
        foreach (Match match in matches.Cast<Match>())
        {
            arguments = arguments.Replace(match.Groups[0].Value, match.Groups[1].Value == "BotToken" ?
                DataProtectionHelper.DecryptDataAsText(config.Client.BotToken, DataProtectionHelper.Base64Encode(config.Client.BotId)) :
                Helper.GetProperty<string, ClientConfig>(config.Client, match.Groups[1].Value)
            );
        }
        return arguments;
    }

    private ConnectionOutputEvent GetOutputEvent(string? outputData)
    {
        if (outputData != null)
        {
            foreach (ConnectionOutputEvent outputEvent in outputEvents.Keys)
            {
                if (outputEvents[outputEvent].Any(outputData.Contains)) { return outputEvent; }
            }
        }
        return ConnectionOutputEvent.Unknown;
    }

    private void OnOutputReceived(object? sender, DataReceivedEventArgs e)
    {
        logger.LogInformation("{data}", e.Data);
        switch (GetOutputEvent(e.Data))
        {
            case ConnectionOutputEvent.Connected:
                Status = ConnectionStatus.Connected;
                mre.Set();
                break;
            case ConnectionOutputEvent.DisconnectedTemporary:
                // raise event for extension
                break;
            case ConnectionOutputEvent.DisconnectedThenRetry:
                Status = ConnectionStatus.Disconnected;
                if (!config.Client.IsAutoReconnect)
                {
                    Disconnect();
                    MessageBoxHelper.ShowErrorFireForget("Disconnected from server");
                }
                mre.Set();
                break;
            case ConnectionOutputEvent.DisconnectedThenExit:
                Status = ConnectionStatus.Disconnected;
                MessageBoxHelper.ShowErrorFireForget("Connection failed. Make sure connected\nto server and bot config is valid!");
                mre.Set();
                break;
        }
    }

    private async void OnConfigChanged(object? sender, EventArgs e)
    {
        if (Status == ConnectionStatus.Connected || config.Client.IsAutoReconnect)
        {
            Disconnect();
            await ReloadConnection();
        }
    }

    private void OnExited(object? sender, EventArgs e)
    {
        Status = ConnectionStatus.Disconnected;
        process.Dispose();
        logger.LogInformation("Jenkins process exited");
    }
}

public class JenkinsEventArgs : EventArgs
{
    public ConnectionStatus Status { get; set; }
    public BotIcon Icon { get; set; }
}