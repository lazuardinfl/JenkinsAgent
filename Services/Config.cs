using Bot.Helpers;
using Bot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bot.Services;

public class Config(ILogger<Config> logger, IHttpClientFactory httpClientFactory)
{
    public bool IsValid { get; private set; } = false;
    public bool IsVersionCompatible { get; private set; } = false;
    public ClientConfig Client { get; private set; } = new();
    public ServerConfig Server { get; private set; } = new();

    public event EventHandler? Reloaded;

    public async Task<bool> Reload(bool raiseEvent = true)
    {
        Directory.CreateDirectory(App.ProfileDir);
        try
        {
            string clientConfig = await File.ReadAllTextAsync($"{App.ProfileDir}/settings.json");
            Client = JsonSerializer.Deserialize<ClientConfig>(clientConfig)!;
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                httpClient.DefaultRequestHeaders.Add("Bot-Hash", App.Hash);
                httpClient.DefaultRequestHeaders.Add("Bot-Version", App.Version);
                string serverConfig = await httpClient.GetStringAsync(Helper.CreateUrl(Client.OrchestratorUrl, Client.SettingsUrl));
                Server = JsonSerializer.Deserialize<ServerConfig>(serverConfig)!;
            }
            IsValid = IsVersionCompatible = true;
        }
        catch (Exception e)
        {
            if ((e is HttpRequestException httpEx) && (httpEx.StatusCode == HttpStatusCode.Unauthorized))
            {
                IsVersionCompatible = false;
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.VersionIncompatible));
            }
            else
            {
                MessageBoxHelper.ShowErrorFireForget(MessageBoxHelper.GetMessage(MessageStatus.ConnectionFailed));
            }
            logger.LogError(e, "{msg}", e.Message);
            IsValid = false;
        }
        if (raiseEvent) { Reloaded?.Invoke(this, EventArgs.Empty); }
        return IsValid;
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

    public async Task Reset()
    {
        Client = new();
        Server = new();
        IsValid = IsVersionCompatible = false;
        await Save();
        Reloaded?.Invoke(this, EventArgs.Empty);
    }
}
