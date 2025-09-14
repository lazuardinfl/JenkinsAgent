using Bot.Helpers;
using Bot.Models;
using Elastic.CommonSchema.Serilog;
using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bot.Services;

public class Config(ILogger<Config> logger, IHttpClientFactory httpClientFactory, SwitchableLogger serilog)
{
    public bool IsValid { get; private set; } = false;
    public bool IsVersionCompatible { get; private set; } = false;
    public ClientConfig Client { get; private set; } = new();
    public ServerConfig Server { get; private set; } = new();

    public event EventHandler? Reloaded;

    public async Task<bool> Reload(bool raiseEvent = true)
    {
        logger.LogInformation("Reloading config");
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
            LoggerConfiguration serilogConf = new();
            serilogConf.Enrich.FromLogContext();
            serilogConf.WriteTo.Async(a => a.File($"{App.ProfileDir}/logs/{App.Title}_v{App.Version}_.log",
                rollingInterval: RollingInterval.Month, fileSizeLimitBytes: 104857600, rollOnFileSizeLimit: true
            ));
            if (Server.LogstashIsEnabled)
            {
                serilogConf.WriteTo.Http(Helper.CreateUrl(Client.OrchestratorUrl, Server.LogstashUrl) ?? Server.LogstashUrl, null,
                    textFormatter: new EcsTextFormatter(new EcsTextFormatterConfiguration { MapCustom = AddEcsDocumentFields })
                );
            }
            if (Server.ElasticsearchIsEnabled)
            {
                serilogConf.WriteTo.Elasticsearch([new Uri(Helper.CreateUrl(Client.OrchestratorUrl, Server.ElasticsearchUrl) ?? Server.ElasticsearchUrl)],
                    options =>
                    {
                        switch (Server.ElasticsearchDataStream.Length)
                        {
                            case 1:
                                options.DataStream = new(Server.ElasticsearchDataStream[0]);
                                break;
                            case 2:
                                options.DataStream = new(Server.ElasticsearchDataStream[0], Server.ElasticsearchDataStream[1]);
                                break;
                            case 3:
                                options.DataStream = new(Server.ElasticsearchDataStream[0], Server.ElasticsearchDataStream[1], Server.ElasticsearchDataStream[2]);
                                break;
                        }
                        options.BootstrapMethod = BootstrapMethod.None;
                        options.TextFormatting = new EcsTextFormatterConfiguration<LogEventEcsDocument> { MapCustom = AddEcsDocumentFields };
                    },
                    transport => transport.Authentication(new ApiKey(Server.ElasticsearchApiKey))
                );
            }
            serilog.Set(serilogConf.CreateLogger(), true);
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
            logger.LogError(e, "{msg:l}", e.Message);
            IsValid = false;
        }
        if (raiseEvent) { Reloaded?.Invoke(this, EventArgs.Empty); }
        return IsValid;
    }

    public async Task<bool> Save()
    {
        logger.LogInformation("Saving config");
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
            logger.LogError(e, "{msg:l}", e.Message);
            return false;
        }
    }

    public async Task Reset()
    {
        logger.LogInformation("Resetting config");
        Client = new();
        Server = new();
        IsValid = IsVersionCompatible = false;
        await Save();
        Reloaded?.Invoke(this, EventArgs.Empty);
    }

    private TEcsDoc AddEcsDocumentFields<TEcsDoc>(TEcsDoc doc, LogEvent log) where TEcsDoc : Elastic.CommonSchema.EcsDocument
    {
        doc.AssignField("bot.id", Client.BotId);
        doc.AssignField("bot.version", App.Version);
        doc.AssignField("bot.elevated", App.IsElevated);
        doc.AssignField("bot.ip", Helper.GetLocalIPAddress());
        doc.AssignField("bot.environment", Environment.GetEnvironmentVariable("APPLICATION_ENVIRONMENT"));
        return doc;
    }
}
