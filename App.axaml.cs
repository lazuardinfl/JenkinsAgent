using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Bot.Helpers;
using Bot.Services;
using Bot.ViewModels;
using Bot.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bot;

public partial class App : Application
{
    public const string DefaultConfigUrl = "public/config/bot.json";
    public static readonly string Title = Helper.GetAppTitle() ?? "Bot";
    public static readonly string Description = Helper.GetAppDescription() ?? "Bot Agent";
    public static readonly Version? Version = Helper.GetAppVersion();
    public static readonly string BaseDir = Helper.GetBaseDir().Replace(@"\", "/");
    public static readonly string ProfileDir = $"{Helper.GetUserDir().Replace(@"\", "/")}/{Title}";
    public static readonly ManualResetEvent Mre = new(false);
    private static readonly Mutex mutex = new(true, Title);
    private readonly IHost host;
    private readonly Config config;
    private readonly AppTray tray;
    private readonly SystemTray systemTray;
    private readonly Agent agent;

    public App()
    {
        SingleInstance();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddEventLog();
        builder.Logging.AddEventSourceLogger();
        builder.Logging.AddConsole();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.SingleLine = false;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss # ";
        });
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<Config>();
        builder.Services.AddSingleton<ScreenSaver>();
        builder.Services.AddSingleton<Jenkins>();
        builder.Services.AddSingleton<Agent>();
        builder.Services.AddSingleton<AppTray>();
        builder.Services.AddSingleton<SystemTray>();
        host = builder.Build();
        config = host.Services.GetRequiredService<Config>();
        agent = host.Services.GetRequiredService<Agent>();
        tray = host.Services.GetRequiredService<AppTray>();
        systemTray = host.Services.GetRequiredService<SystemTray>();
    }

    public override void Initialize()
    {
        agent.Initialize();
        tray.Initialize();
        systemTray.Initialize();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(config)
            };
            tray.RegisterShowMainWindow((MainWindowViewModel)app.MainWindow.DataContext);
        }
        base.OnFrameworkInitializationCompleted();
        Mre.Set();
    }

    public static IClassicDesktopStyleApplicationLifetime Lifetime() => (IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!;

    public static Dispatcher GetUIThread() => Dispatcher.UIThread;

    public static async Task Exit() => await GetUIThread().InvokeAsync(() => Lifetime().Shutdown());

    private void SingleInstance()
    {
        if (!mutex.WaitOne(0, false))
        {
            MessageBoxHelper.ShowError("Application already running!");
            mutex.Dispose();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
            {
                app.Shutdown();
            }
            Environment.Exit(0);
        }
    }
}