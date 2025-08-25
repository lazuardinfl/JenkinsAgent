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

public enum ExtensionStatus { Valid, Invalid, Expired, GracePeriod }

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
        UpdatePreventLockStatus(await GetPreventLockExpiredDate());
        UpdatePreventLockStatus(PreventLockExpiredDate);
        ReloadPreventLock();
    }

    public void ReloadPreventLock()
    {
        switch (PreventLockStatus, config.Client.IsPreventLock)
        {
            case (ExtensionStatus.Valid or ExtensionStatus.GracePeriod, true):
                SetScreenSaverTimeout(config.Server.ScreenSaverTimeout);
                timer.Enabled = true;
                logger.LogInformation("Prevent Lock is {msg}", PreventLockStatus is ExtensionStatus.Valid ? "running" : "in grace period");
                break;
            default:
                timer.Enabled = false;
                logger.LogInformation("Prevent Lock is not running");
                break;
        }
        PreventLockStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePreventLockStatus(DateTime? expiredDate)
    {
        try
        {
            switch (PreventLockStatus, (int?)(expiredDate is null ? null : DateTime.Now.CompareTo(expiredDate)))
            {
                case (ExtensionStatus.Valid, null):
                    DateTime gracePeriod = DateTime.Now.AddHours(config.Server.ScreenSaverGracePeriod);
                    if (gracePeriod.CompareTo(PreventLockExpiredDate) < 0)
                    {
                        PreventLockExpiredDate = gracePeriod;
                        PreventLockStatus = ExtensionStatus.GracePeriod;
                    }
                    break;
                case (ExtensionStatus.Invalid or ExtensionStatus.Expired, null):
                    PreventLockExpiredDate = null;
                    PreventLockStatus = ExtensionStatus.Invalid;
                    break;
                case (ExtensionStatus.Valid or ExtensionStatus.Invalid or ExtensionStatus.Expired, <= 0):
                case (ExtensionStatus.GracePeriod, <= 0) when (expiredDate is DateTime exp) && (exp.CompareTo(PreventLockExpiredDate) != 0):
                    PreventLockExpiredDate = expiredDate;
                    PreventLockStatus = ExtensionStatus.Valid;
                    break;
                case (_, > 0):
                    PreventLockExpiredDate = expiredDate;
                    PreventLockStatus = ExtensionStatus.Expired;
                    break;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
            PreventLockStatus = ExtensionStatus.Invalid;
        }
    }

    private async Task<DateTime?> GetPreventLockExpiredDate()
    {
        if (config.IsValid || config.IsVersionCompatible)
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
        if (DateTime.Now.Subtract(lastUpdate).TotalMinutes >= config.Server.ScreenSaverUpdateInterval)
        {
            lastUpdate = DateTime.Now;
            UpdatePreventLockStatus(await GetPreventLockExpiredDate());
            PreventLockStatusChanged?.Invoke(this, EventArgs.Empty);
        }
        UpdatePreventLockStatus(PreventLockExpiredDate);
        switch (PreventLockStatus)
        {
            case ExtensionStatus.Valid or ExtensionStatus.GracePeriod:
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
        if (!config.IsVersionCompatible) { PreventLockStatus = ExtensionStatus.Invalid; }
        Initialize();
    }

    private void ResetLockScreenTimer()
    {
        try
        {
            // prevent display turned off
            _ = SetThreadExecutionState(0x00000002 | 0x80000000);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{msg}", e.Message);
        }
    }

    private void SetScreenSaverTimeout(int timeout)
    {
        try
        {
            // screen saver timeout in seconds
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
