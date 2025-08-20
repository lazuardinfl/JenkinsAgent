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

public enum ConnectionStatus { Connected, Disconnected, Initialize, Retry, Interrupted, Unknown }

public class Jenkins
{
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Config config;
    private readonly ManualResetEvent mre;
    private readonly Dictionary<ConnectionStatus, string[]> outputStreams;
    private ConnectionStatus status;
    private Process? process;

    public Jenkins(ILogger<Jenkins> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        mre = new(false);
        outputStreams = new()
        {
            { ConnectionStatus.Connected, ["INFO: Connected"] },
            { ConnectionStatus.Interrupted, ["Write side closed"] },
            { ConnectionStatus.Retry, ["is not ready", "seconds before retry"] },
            { ConnectionStatus.Disconnected, [
                "buffer too short", "For input string", "Invalid byte", "takes an operand",
                "No subject alternative DNS", "SEVERE: Handshake error"
            ]}
        };
        status = ConnectionStatus.Disconnected;
        config.Reloaded += OnConfigReloaded;
    }

    public event EventHandler? ConnectionChanged;

    public ConnectionStatus Status
    {
        get => status;
        private set
        {
            if (status != value && status != ConnectionStatus.Unknown)
            {
                status = value;
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public async Task Connect(bool atStartup = false)
    {
        if (await Initialize())
        {
            try
            {
                mre.Reset();
                process = new();
                process.StartInfo.FileName = $"{App.ProfileDir}/{config.Server.JavaPath}/java.exe";
                process.StartInfo.Arguments = (config.Client.IsWindowsCertStoreUsed ? "-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT " : "") +
                                              $"-jar {config.Server.AgentPath} {config.Server.AgentArguments ?? "-secret"}";
                MatchCollection matches = Helper.AngleBracketsRegex().Matches(process.StartInfo.Arguments);
                foreach (Match match in matches.Cast<Match>())
                {
                    process.StartInfo.Arguments = process.StartInfo.Arguments.Replace(match.Groups[0].Value, match.Groups[1].Value == "BotToken" ?
                        CryptographyHelper.DecryptWithDPAPI(config.Client.BotToken, CryptographyHelper.Base64Encode(config.Client.BotId)) :
                        Helper.GetProperty<string, ClientConfig>(config.Client, match.Groups[1].Value)
                    );
                }
                process.StartInfo.WorkingDirectory = App.ProfileDir;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += OnOutputReceived;
                process.ErrorDataReceived += OnOutputReceived;
                process.Exited += OnExited;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                logger.LogInformation("Jenkins PID {pid} started", process.Id);
                if (!await Task.Run(() => mre.WaitOne(atStartup ? config.Server.StartupConnectTimeout : config.Server.ConnectTimeout)))
                {
                    Disconnect();
                    MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed));
                }
            }
            catch (Exception e)
            {
                Status = ConnectionStatus.Disconnected;
                logger.LogError(e, "{msg}", e.Message);
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.UnexpectedError));
            }
        }
    }

    public void Disconnect()
    {
        try
        {
            logger.LogInformation("Jenkins disconnected");
            if (process is not null)
            {
                logger.LogInformation("Jenkins PID {pid} exited", process.Id);
                process.CancelOutputRead();
                process.CancelErrorRead();
                process.Kill(config.Server.KillProcessTreeOnExit);
                process.Close();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
        Status = ConnectionStatus.Disconnected;
    }

    private async Task<bool> Initialize()
    {
        bool isReady = false;
        Status = ConnectionStatus.Initialize;
        // check config
        if (config.IsVersionCompatible)
        {
            // check java
            bool isJavaReady = IsJavaVersionCompatible() || await DownloadJava();
            // check agent
            bool isAgentReady = isJavaReady && (IsAgentVersionCompatible() || await DownloadAgent());
            // assign ready status
            isReady = isJavaReady && isAgentReady;
        }
        Status = isReady ? ConnectionStatus.Initialize : ConnectionStatus.Disconnected;
        if (!isReady) { MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed)); }
        return isReady;
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
        logger.LogInformation("Downloading Java");
        string javaDir = $"{App.ProfileDir}/{Path.GetDirectoryName(config.Server.JavaPath)}";
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            using (HttpResponseMessage response = await httpClient.GetAsync(Helper.CreateUrl(config.Client.OrchestratorUrl, config.Server.JavaUrl)))
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            using (ZipArchive archive = new(stream))
            {
                if (Directory.Exists(javaDir)) { Directory.Delete(javaDir, true); }
                archive.ExtractToDirectory(App.ProfileDir, true);
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
        logger.LogInformation("Downloading Agent");
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            using (HttpResponseMessage response = await httpClient.GetAsync(Helper.CreateUrl(config.Client.OrchestratorUrl, config.Server.AgentUrl)))
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            using (FileStream file = File.Create($"{App.ProfileDir}/{config.Server.AgentPath}"))
            {
                stream.CopyTo(file);
            }
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private ConnectionStatus GetOutputStreamStatus(string? outputData)
    {
        if (outputData != null)
        {
            foreach (ConnectionStatus output in outputStreams.Keys)
            {
                if (outputStreams[output].Any(outputData.Contains)) { return output; }
            }
        }
        return ConnectionStatus.Unknown;
    }

    private async void OnOutputReceived(object? sender, DataReceivedEventArgs e)
    {
        logger.LogInformation("{data}", e.Data);
        switch (GetOutputStreamStatus(e.Data))
        {
            case ConnectionStatus.Connected:
                mre.Set();
                Status = ConnectionStatus.Connected;
                break;
            case ConnectionStatus.Interrupted:
                Status = ConnectionStatus.Interrupted;
                if (!await config.Reload() || !config.IsVersionCompatible || !config.Client.IsAutoReconnect) { Disconnect(); }
                break;
            case ConnectionStatus.Retry:
                mre.Set();
                if (config.Client.IsAutoReconnect) { Status = ConnectionStatus.Retry; }
                else
                {
                    Disconnect();
                    MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed));
                }
                break;
            case ConnectionStatus.Disconnected:
                mre.Set();
                Status = ConnectionStatus.Disconnected;
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed));
                break;
        }
    }

    private async void OnConfigReloaded(object? sender, EventArgs e)
    {
        if (Status != ConnectionStatus.Interrupted && (Status != ConnectionStatus.Disconnected || config.Client.IsAutoReconnect))
        {
            Disconnect();
            if (config.IsVersionCompatible) { await Connect(); }
        }
    }

    private void OnExited(object? sender, EventArgs e)
    {
        Status = ConnectionStatus.Disconnected;
        try
        {
            process?.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{msg}", ex.Message);
        }
    }
}
