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
    private ConnectionStatus status;
    private Process process;

    public JenkinsCredential Credential { get; set; } = new();
    public JenkinsConfig Config { get; set; } = new();
    public bool IsAutoReconnect { get; set; }

    public Jenkins(ILogger<Jenkins> logger, IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        process = new();
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

    private bool IsJavaVersionCompatible()
    {
        try
        {
            string? localJavaVersion = FileVersionInfo.GetVersionInfo($"{App.BaseDir}/{Config.JavaPath}/java.exe").FileVersion;
            return localJavaVersion == Config.JavaVersion;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    private async Task<bool> DownloadJava()
    {
        string javaDir = $"{App.BaseDir}/{Path.GetDirectoryName(Config.JavaPath)}";
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                using (HttpResponseMessage response = await httpClient.GetAsync($"{Credential.Url}/{Config.JavaUrl}"))
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
                process.StartInfo.FileName = $"{App.BaseDir}/{Config.JavaPath}/java.exe";
                process.StartInfo.Arguments = $"-jar {Config.AgentPath} -version";
                process.StartInfo.WorkingDirectory = App.BaseDir;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                string localAgentVersion = process.StandardOutput.ReadToEnd();
                return localAgentVersion.Contains(Config.AgentVersion!);
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
                using (HttpResponseMessage response = await httpClient.GetAsync($"{Credential.Url}/{Config.AgentUrl}"))
                {
                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream file = File.Create($"{App.BaseDir}/{Config.AgentPath}"))
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
            process.StartInfo.FileName = $"{App.BaseDir}/{Config.JavaPath}/java.exe";
            process.StartInfo.Arguments = $"-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT -jar {Config.AgentPath} " +
                                            $"-jnlpUrl {Credential.Url}/computer/{Credential.Id}/jenkins-agent.jnlp " +
                                            $"-secret {Credential.Token}";
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
            await Task.Run(() => mre.WaitOne(Config.ConnectTimeout ?? 10000));
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
            if (!IsAutoReconnect)
            {
                Disonnect(false);
                App.RunOnUIThread(async () => {
                    await MessageBox.InvalidJenkinsCredential().ShowAsync();
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
                IsAutoReconnect = IsAutoReconnect
            };
            ConnectionChanged?.Invoke(this, args);
        }
    }

    public event EventHandler<JenkinsEventArgs>? ConnectionChanged;
}