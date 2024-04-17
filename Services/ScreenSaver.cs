using Bot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Bot.Services;

public class ScreenSaver(ILogger<ScreenSaver> logger, IHttpClientFactory httpClientFactory, Config config)
{
    public async void Initialize()
    {
        KeyValuePair<string, string?>[] content = [
            new KeyValuePair<string, string?>("client_id", config.Server.ExtensionAuthId),
            new KeyValuePair<string, string?>("client_secret", config.Server.ExtensionAuthSecret),
            new KeyValuePair<string, string?>("grant_type", "password"),
            new KeyValuePair<string, string?>("username", config.Client.BotId),
            new KeyValuePair<string, string?>("password", config.Client.BotToken)
        ];
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                using (HttpResponseMessage response = await httpClient.PostAsync(config.Server.ExtensionAuthUrl, new FormUrlEncodedContent(content)))
                {
                    JsonNode res = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                    JsonWebToken token = new(res["access_token"]?.GetValue<string>());
                    string[] info = token.GetPayloadValue<string[]>("info");
                    foreach (var item in info)
                    {
                        logger.LogInformation(item);
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }
}