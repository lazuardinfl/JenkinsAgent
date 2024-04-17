using Bot.Helpers;
using Bot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services;

public class Agent(ILogger<Agent> logger, IHttpClientFactory httpClientFactory, Config config, Jenkins jenkins, ScreenSaver screenSaver)
{
    public static readonly ManualResetEvent Mre = new(false);

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
        screenSaver.Initialize();
    }

    public async Task<bool> ReloadConfig()
    {
        try
        {
            string clientConfig = await File.ReadAllTextAsync($"{App.BaseDir}/settings.json");
            config.Client = JsonSerializer.Deserialize<ClientConfig>(clientConfig)!;
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                string serverConfig = await httpClient.GetStringAsync($"{config.Client.OrchestratorUrl}/{config.Client.SettingsUrl}");
                config.Server = JsonSerializer.Deserialize<ServerConfig>(serverConfig)!;
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
            await using (FileStream stream = File.Create($"{App.BaseDir}/settings.json"))
            {
                await JsonSerializer.SerializeAsync(stream, config.Client, Helper.JsonOptions);
            }
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }
}