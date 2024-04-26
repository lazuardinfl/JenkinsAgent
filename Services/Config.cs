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

    public void RaiseChanged(object? sender, EventArgs e) => Changed?.Invoke(sender, e);

    public async Task<bool> Reload(bool showMessageBox = false)
    {
        Directory.CreateDirectory(App.ProfileDir);
        try
        {
            string clientConfig = await File.ReadAllTextAsync($"{App.ProfileDir}/settings.json");
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
                MessageBoxHelper.ShowErrorFireForget("Connection failed. Make sure connected\nto server and bot config is valid!");
            }
            logger.LogError(e, "{msg}", e.Message);
            return false;
        }
    }

    public async Task<bool> Save()
    {
        Directory.CreateDirectory(App.ProfileDir);
        try
        {
            await using (FileStream stream = File.Create($"{App.ProfileDir}/settings.json"))
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