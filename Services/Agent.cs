using Bot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Agent
{
    public static readonly ManualResetEvent Mre = new(false);
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Jenkins jenkins;
    private string configUrl = "public/config/bot.json";

    public Agent(ILogger<Agent> logger, IHttpClientFactory httpClientFactory, Jenkins jenkins)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.jenkins = jenkins;
    }

    public async void Initialize()
    {
        bool isConfigValid = await ReloadConfig();
        Mre.Set();
        await Task.Run(App.Mre.WaitOne);
        if (!(isConfigValid && await jenkins.Initialize() && await jenkins.Connect()))
        {
            App.RunOnUIThread(async () => {
                await MessageBox.Error("Connection failed. Make sure connected\n" +
                                        "to server and bot config is valid!").ShowAsync();
            });
        }
    }

    public async Task<bool> ReloadConfig()
    {
        try
        {
            string localConfig = await File.ReadAllTextAsync($"{App.BaseDir}/settings.json");
            JsonNode localConfigJson = JsonNode.Parse(localConfig)!;
            configUrl = localConfigJson["ConfigUrl"]?.GetValue<string>() ?? configUrl;
            jenkins.IsAutoReconnect = localConfigJson["AutoReconnect"]?.GetValue<bool>() ?? true;
            jenkins.Credential = JsonSerializer.Deserialize<JenkinsCredential>(localConfig)!;
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                string serverConfig = await httpClient.GetStringAsync($"{jenkins.Credential.Url}/{configUrl}");
                jenkins.Config = JsonSerializer.Deserialize<JenkinsConfig>(serverConfig)!;
            }
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            jenkins.Status = ConnectionStatus.Disconnected;
            return false;
        }
    }

    public async Task<bool> SaveConfig()
    {
        try
        {
            JsonObject config = new()
            {
                ["OrchestratorUrl"] = jenkins.Credential.Url,
                ["ConfigUrl"] = configUrl,
                ["BotId"] = jenkins.Credential.Id,
                ["BotToken"] = jenkins.Credential.Token,
                ["AutoReconnect"] = jenkins.IsAutoReconnect
            };
            await File.WriteAllTextAsync($"{App.BaseDir}/settings.json", config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }
}