using Bot.Helpers;
using Bot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bot.Services;

public class Config(ILogger<Config> logger, IHttpClientFactory httpClientFactory)
{
    public event EventHandler? Changed;

    public ClientConfig Client { get; set; } = new();
    public ServerConfig Server { get; set; } = new();

    public void RaiseChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(sender, e);
    }

    public async Task<bool> Reload(bool showMessageBox = false)
    {
        try
        {
            string clientConfig = await File.ReadAllTextAsync($"{App.BaseDir}/settings.json");
            Client = JsonSerializer.Deserialize<ClientConfig>(clientConfig)!;
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                string serverConfig = await httpClient.GetStringAsync($"{Client.OrchestratorUrl}/{Client.SettingsUrl}");
                Server = JsonSerializer.Deserialize<ServerConfig>(serverConfig)!;
            }
            return true;
        }
        catch (Exception e)
        {
            if (showMessageBox)
            {
                App.GetUIThread().Post(async () => {
                    await MessageBox.Error("Connection failed. Make sure connected\n" +
                                           "to server and bot config is valid!").ShowAsync();
                });
            }
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    public async Task<bool> Save()
    {
        try
        {
            await using (FileStream stream = File.Create($"{App.BaseDir}/settings.json"))
            {
                await JsonSerializer.SerializeAsync(stream, Client, Helper.JsonOptions);
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