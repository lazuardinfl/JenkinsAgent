using Bot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Jenkins
{
    private static readonly ManualResetEvent mre = new(false);
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Config config;
    private ConnectionStatus status = ConnectionStatus.Disconnected;
    private Process process = null!;

    public Jenkins(ILogger<Jenkins> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        config.Changed += OnConfigChanged;
    }

    public event EventHandler<JenkinsEventArgs>? ConnectionChanged;

    public ConnectionStatus Status
    {
        get { return status; }
        set
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

    public async Task<bool> Connect()
    {
        try
        {
            mre.Reset();
            process = new();
            process.StartInfo.FileName = $"{App.ProfileDir}/{config.Server.JavaPath}/java.exe";
            process.StartInfo.Arguments = $"-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT -jar {config.Server.AgentPath} " +
                                            $"-jnlpUrl {config.Client.OrchestratorUrl}/computer/{config.Client.BotId}/jenkins-agent.jnlp " +
                                            $"-secret {config.Client.BotToken}";
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
            await Task.Run(() => mre.WaitOne(config.Server.ConnectTimeout ?? 10000));
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
        if (Status != ConnectionStatus.Connected) { Disconnect(); }
        return Status == ConnectionStatus.Connected;
    }

    public void Disconnect(bool setStatus = true)
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
        finally
        {
            if (setStatus) { Status = ConnectionStatus.Disconnected; }
        }
    }

    public async Task<bool> ReloadConnection(bool showMessageBox = false)
    {
        bool isConnected = await Initialize() && await Connect();
        if (!isConnected && showMessageBox)
        {
            App.GetUIThread().Post(async () => {
                await MessageBox.Error("Connection failed. Make sure connected\n" +
                                       "to server and bot config is valid!").ShowAsync();
            });
        }
        return isConnected;
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
                using (HttpResponseMessage response = await httpClient.GetAsync($"{config.Client.OrchestratorUrl}/{config.Server.JavaUrl}"))
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
                using (HttpResponseMessage response = await httpClient.GetAsync($"{config.Client.OrchestratorUrl}/{config.Server.AgentUrl}"))
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

    private async void OnConfigChanged(object? sender, EventArgs e)
    {
        if (Status == ConnectionStatus.Connected || config.Client.IsAutoReconnect)
        {
            Disconnect(false);
            await ReloadConnection(true);
        }
    }

    private void OnOutputReceived(object? sender, DataReceivedEventArgs e)
    {
        logger.LogInformation("{data}", e.Data);
        // null to prevent exception
        if (e.Data is null) {}
        // connected first time or after disconnected
        else if (e.Data.Contains("INFO: Connected"))
        {
            Status = ConnectionStatus.Connected;
            mre.Set();
        }
        // disconnected at first time
        else if (e.Data.Contains("Failed to obtain") || e.Data.Contains("buffer too short") ||
                 e.Data.Contains("SEVERE: Handshake error") || e.Data.Contains("Invalid byte"))
        {
            mre.Set();
        }
        // disconnected in the middle connection
        else if (e.Data.Contains("is not ready"))
        {
            if (Status != ConnectionStatus.Disconnected) { Status = ConnectionStatus.Disconnected; }
            if (!config.Client.IsAutoReconnect)
            {
                Disconnect(false);
                App.GetUIThread().Post(async () => {
                    await MessageBox.Error("Disconnected from server").ShowAsync();
                });
            }
        }
    }

    private void OnExited(object? sender, EventArgs e)
    {
        process.Dispose();
        logger.LogInformation("Jenkins process exited");
    }
}

public class JenkinsEventArgs : EventArgs
{
    public ConnectionStatus Status { get; set; }
    public BotIcon Icon { get; set; }
}