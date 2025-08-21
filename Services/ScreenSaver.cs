using Bot.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Timers;

namespace Bot.Services;

public enum ExtensionStatus { Valid, Invalid, Expired }

public class ScreenSaver
{
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Config config;
    private readonly Timer timer;
    private DateTime lastUpdate;

    public DateTime? PreventLockExpiredDate { get; private set; }
    public ExtensionStatus PreventLockStatus { get; private set; }

    public ScreenSaver(ILogger<ScreenSaver> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        timer = new(config.Server.ScreenSaverTimerInterval);
        lastUpdate = DateTime.Now;
        PreventLockStatus = ExtensionStatus.Invalid;
        config.Reloaded += OnConfigReloaded;
        timer.Elapsed += OnTimedEvent;
    }

    public event EventHandler? PreventLockStatusChanged;

    public async void Initialize()
    {
        timer.Interval = config.Server.ScreenSaverTimerInterval;
        PreventLockExpiredDate = await GetPreventLockExpiredDate();
        PreventLockStatus = GetPreventLockStatus(PreventLockExpiredDate);
        ReloadPreventLock();
    }

    public void ReloadPreventLock()
    {
        switch (PreventLockStatus, config.Client.IsPreventLock)
        {
            case (ExtensionStatus.Valid, true):
                SetScreenSaverTimeout(config.Server.ScreenSaverTimeout);
                timer.Enabled = true;
                logger.LogInformation("Prevent Lock running");
                break;
            default:
                timer.Enabled = false;
                logger.LogInformation("Prevent Lock not running");
                break;
        }
        PreventLockStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private ExtensionStatus GetPreventLockStatus(DateTime? expiredDate)
    {
        try
        {
            switch ((int?)(expiredDate == null ? null : DateTime.Now.CompareTo(expiredDate)))
            {
                case <= 0:
                    return ExtensionStatus.Valid;
                case > 0:
                    return ExtensionStatus.Expired;
                case null:
                    return ExtensionStatus.Invalid;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return ExtensionStatus.Invalid;
        }
    }

    private async Task<DateTime?> GetPreventLockExpiredDate()
    {
        if (config.IsValid)
        {
            Dictionary<string, string?> content = new()
            {
                { "client_id", config.Server.ExtensionAuthId },
                { "client_secret", config.Server.ExtensionAuthSecret },
                { "grant_type", "password" },
                { "username", config.Client.BotId },
                { "password", CryptographyHelper.DecryptWithDPAPI(config.Client.BotToken, CryptographyHelper.Base64Encode(config.Client.BotId)) }
            };
            try
            {
                using (HttpClient httpClient = httpClientFactory.CreateClient())
                using (HttpResponseMessage response = await httpClient.PostAsync(Helper.CreateUrl(config.Client.OrchestratorUrl, config.Server.ExtensionAuthUrl), new FormUrlEncodedContent(content)))
                {
                    JsonNode jsonResponse = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                    JsonWebToken token = new(jsonResponse["access_token"]?.GetValue<string>());
                    string[] info = token.GetPayloadValue<string[]>("info");
                    foreach (var extension in info)
                    {
                        if (extension.Contains("PreventLock"))
                        {
                            return DateTime.ParseExact(extension.Split('@')[1], "yyyyMMdd", CultureInfo.InvariantCulture).Add(new TimeSpan(23, 59, 59));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "{msg}", e.Message);
            }
        }
        return null;
    }

    private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (DateTime.Now.Subtract(lastUpdate).TotalHours >= 24)
        {
            lastUpdate = DateTime.Now;
            PreventLockExpiredDate = await GetPreventLockExpiredDate();
        }
        PreventLockStatus = GetPreventLockStatus(PreventLockExpiredDate);
        switch (PreventLockStatus)
        {
            case ExtensionStatus.Valid:
                ResetLockScreenTimer();
                break;
            default:
                ReloadPreventLock();
                break;
        }
    }

    private void OnConfigReloaded(object? sender, EventArgs e)
    {
        lastUpdate = DateTime.Now;
        Initialize();
    }

    private void ResetLockScreenTimer()
    {
        try
        {
            _ = SetThreadExecutionState(0x00000002 | 0x80000000);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    // Pass in the number of seconds to set the screen saver timeout value.
    private void SetScreenSaverTimeout(int timeout)
    {
        try
        {
            int nullVar = 0;
            SystemParametersInfo(15, timeout, ref nullVar, 2);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(int uAction, int uParam, ref int lpvParam, int flags);
}
