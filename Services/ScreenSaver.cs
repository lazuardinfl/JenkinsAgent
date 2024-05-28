using Bot.Helpers;
using Bot.Models;
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

public class ScreenSaver
{
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Config config;
    private readonly Timer timer = new(50000);
    private DateTime lastUpdate = DateTime.Now;
    private DateTime? preventLockExpiredDate = null;
    private ExtensionStatus preventLockStatus = ExtensionStatus.Invalid;

    public ScreenSaver(ILogger<ScreenSaver> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        config.Reloaded += OnConfigReloaded;
        timer.Elapsed += OnTimedEvent;
    }

    public event EventHandler<ScreenSaverEventArgs>? PreventLockStatusChanged;

    [Flags]
    private enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    public async void Initialize()
    {
        timer.Interval = config.Server.ScreenSaverTimerInterval;
        preventLockExpiredDate = await GetPreventLockExpiredDate();
        preventLockStatus = GetPreventLockStatus(preventLockExpiredDate);
        ReloadPreventLock();
    }

    public void ReloadPreventLock()
    {
        switch (preventLockStatus, config.Client.IsPreventLock)
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
        ScreenSaverEventArgs args = new()
        {
            PreventLockStatus = preventLockStatus,
            PreventLockExpiredDate = preventLockExpiredDate
        };
        PreventLockStatusChanged?.Invoke(this, args);
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
                {
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
            }
            catch (Exception e)
            {
                logger.LogError(e, "{msg}", e.Message);
                return null;
            }
        }
        return null;
    }

    private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (DateTime.Now.Subtract(lastUpdate).TotalHours >= 24)
        {
            lastUpdate = DateTime.Now;
            preventLockExpiredDate = await GetPreventLockExpiredDate();
        }
        preventLockStatus = GetPreventLockStatus(preventLockExpiredDate);
        switch (preventLockStatus)
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
            SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
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
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(int uAction, int uParam, ref int lpvParam, int flags);
}

public class ScreenSaverEventArgs : EventArgs
{
    public ExtensionStatus PreventLockStatus { get; set; }
    public DateTime? PreventLockExpiredDate { get; set; }
}