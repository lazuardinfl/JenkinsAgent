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
    private ExtensionStatus preventLockStatus = ExtensionStatus.Invalid;
    private DateTime preventLockExpiredDate;

    public ScreenSaver(ILogger<ScreenSaver> logger, IHttpClientFactory httpClientFactory, Config config)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.config = config;
        config.Changed += OnConfigChanged;
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
        timer.Interval = config.Server.ScreenSaverTimerInterval ?? timer.Interval;
        preventLockStatus = await GetPreventLockStatus();
        SetPreventLock();
    }

    public void SetPreventLock()
    {
        switch (preventLockStatus, config.Client.IsPreventLock)
        {
            case (ExtensionStatus.Valid, true):
                SetScreenSaverTimeout(config.Server.ScreenSaverTimeout ?? 600);
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

    private async void OnConfigChanged(object? sender, EventArgs e)
    {
        preventLockStatus = await GetPreventLockStatus();
        SetPreventLock();
    }

    private void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (DateTime.Now.CompareTo(preventLockExpiredDate) <= 0)
        {
            ResetLockScreenTimer();
        }
        else
        {
            preventLockStatus = ExtensionStatus.Expired;
            SetPreventLock();
        }
    }

    private async Task<ExtensionStatus> GetPreventLockStatus(bool setExpiredDate = true)
    {
        Dictionary<string, string?> content = new()
        {
            { "client_id", config.Server.ExtensionAuthId },
            { "client_secret", config.Server.ExtensionAuthSecret },
            { "grant_type", "password" },
            { "username", config.Client.BotId },
            { "password", config.Client.BotToken }
        };
        try
        {
            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                using (HttpResponseMessage response = await httpClient.PostAsync(config.Server.ExtensionAuthUrl, new FormUrlEncodedContent(content)))
                {
                    JsonNode jsonResponse = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                    JsonWebToken token = new(jsonResponse["access_token"]?.GetValue<string>());
                    string[] info = token.GetPayloadValue<string[]>("info");
                    foreach (var extension in info)
                    {
                        if (extension.Contains("PreventLock"))
                        {
                            DateTime expire = DateTime.ParseExact(extension.Split('@')[1], "yyyyMMdd", CultureInfo.InvariantCulture).Add(new TimeSpan(23, 59, 59));
                            preventLockExpiredDate = setExpiredDate == true ? expire : preventLockExpiredDate;
                            return DateTime.Now.CompareTo(expire) <= 0 ? ExtensionStatus.Valid : ExtensionStatus.Expired;
                        }
                    }
                }
            }
            return ExtensionStatus.Invalid;
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            return ExtensionStatus.Invalid;
        }
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
    public DateTime PreventLockExpiredDate { get; set; }
}