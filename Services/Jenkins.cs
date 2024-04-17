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

public class Jenkins(ILogger<Jenkins> logger, IHttpClientFactory httpClientFactory, Config config)
{
    private static readonly ManualResetEvent mre = new(false);
    private Process process = new();
    private ConnectionStatus status;

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

    private bool IsJavaVersionCompatible()
    {
        try
        {
            string? localJavaVersion = FileVersionInfo.GetVersionInfo($"{App.BaseDir}/{config.Server.JavaPath}/java.exe").FileVersion;
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
        string javaDir = $"{App.BaseDir}/{Path.GetDirectoryName(config.Server.JavaPath)}";
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
                            archive.ExtractToDirectory(App.BaseDir, true);
                        }
                    }
                }
            }
            DirectoryInfo javaTemp = new DirectoryInfo(App.BaseDir).GetDirectories().OrderByDescending(d => d.LastWriteTimeUtc).First();
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
                process.StartInfo.FileName = $"{App.BaseDir}/{config.Server.JavaPath}/java.exe";
                process.StartInfo.Arguments = $"-jar {config.Server.AgentPath} -version";
                process.StartInfo.WorkingDirectory = App.BaseDir;
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
                        using (FileStream file = File.Create($"{App.BaseDir}/{config.Server.AgentPath}"))
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

    public async Task<bool> Connect(bool newProcess = false)
    {
        try
        {
            if (newProcess) { process = new(); }
            mre.Reset();
            process.StartInfo.FileName = $"{App.BaseDir}/{config.Server.JavaPath}/java.exe";
            process.StartInfo.Arguments = $"-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT -jar {config.Server.AgentPath} " +
                                            $"-jnlpUrl {config.Client.OrchestratorUrl}/computer/{config.Client.BotId}/jenkins-agent.jnlp " +
                                            $"-secret {config.Client.BotToken}";
            process.StartInfo.WorkingDirectory = App.BaseDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += new DataReceivedEventHandler(OutputReceived);
            process.ErrorDataReceived += new DataReceivedEventHandler(OutputReceived);
            process.Exited += new EventHandler(Exited);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.Run(() => mre.WaitOne(config.Server.ConnectTimeout ?? 10000));
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
        if (Status != ConnectionStatus.Connected) { Disonnect(); }
        return Status == ConnectionStatus.Connected;
    }

    public void Disonnect(bool setStatus = true)
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

    private void OutputReceived(object? sender, DataReceivedEventArgs e)
    {
        logger.LogInformation("{data}", e.Data);
        if (e.Data is null) {}
        else if (e.Data.Contains("INFO: Connected"))
        {
            Status = ConnectionStatus.Connected;
            mre.Set();
        }
        else if (e.Data.Contains("Failed to obtain") || e.Data.Contains("buffer too short") || e.Data.Contains("SEVERE: Handshake error"))
        {
            mre.Set();
        }
        else if (e.Data.Contains("is not ready"))
        {
            if (Status != ConnectionStatus.Disconnected) { Status = ConnectionStatus.Disconnected; }
            if (!config.Client.IsAutoReconnect)
            {
                Disonnect(false);
                App.RunOnUIThread(async () => {
                    await MessageBox.Error("Diconnected from server").ShowAsync();
                });
            }
        }
    }

    private void Exited(object? sender, EventArgs e)
    {
        process.Dispose();
        logger.LogInformation("Jenkins process exited");
    }

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
                IsAutoReconnect = config.Client.IsAutoReconnect
            };
            ConnectionChanged?.Invoke(this, args);
        }
    }

    public event EventHandler<JenkinsEventArgs>? ConnectionChanged;
}

public class JenkinsEventArgs : EventArgs
{
    public ConnectionStatus Status { get; set; }
    public BotIcon Icon { get; set; }
    public bool IsAutoReconnect { get; set; }
}