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
    private readonly Dictionary<ConnectionStatus, string[]> outputStreams;
    private ConnectionStatus status = ConnectionStatus.Disconnected;
    private Process process = null!;

    public Jenkins(ILogger<Jenkins> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        config.Changed += OnConfigChanged;
        outputStreams = new()
        {
            { ConnectionStatus.Connected, ["INFO: Connected"] },
            { ConnectionStatus.Interrupted, ["Write side closed"] },
            { ConnectionStatus.Retry, ["Failed to obtain", "is not ready"] },
            { ConnectionStatus.Disconnected, [
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
            if (status != value && status != ConnectionStatus.Unknown)
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
        bool isReady = false;
        // check config
        if (config.IsValid)
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
            isReady = isJavaReady && isAgentReady;
        }
        await Task.Run(App.Mre.WaitOne);
        Status = isReady ? ConnectionStatus.Retry : ConnectionStatus.Disconnected;
        if (!isReady) { MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed)); }
        return isReady;
    }

    public async Task Connect(bool atStartup = false)
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

    private void OnOutputReceived(object? sender, DataReceivedEventArgs e)
    {
        logger.LogInformation("{data}", e.Data);
        switch (GetOutputStreamStatus(e.Data))
        {
            case ConnectionStatus.Connected:
                mre.Set();
                Status = ConnectionStatus.Connected;
                break;
            case ConnectionStatus.Interrupted:
                // raise event for extension
                Status = ConnectionStatus.Interrupted;
                break;
            case ConnectionStatus.Retry:
                mre.Set();
                if (config.Client.IsAutoReconnect)
                {
                    Status = ConnectionStatus.Retry;
                }
                else
                {
                    string msg = Status == ConnectionStatus.Retry ?
                        MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed) :
                        "Disconnected from server";
                    Disconnect();
                    MessageBoxHelper.ShowErrorFireForget(msg);
                }
                break;
            case ConnectionStatus.Disconnected:
                mre.Set();
                Status = ConnectionStatus.Disconnected;
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed));
                break;
        }
    }

    private async void OnConfigChanged(object? sender, EventArgs e)
    {
        if (Status == ConnectionStatus.Connected || Status == ConnectionStatus.Retry || config.Client.IsAutoReconnect)
        {
            Disconnect();
            if (config.IsValid && await Initialize()) { await Connect(); }
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