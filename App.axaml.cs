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
using System.IO;
using System.Threading;

namespace Bot;

public partial class App : Application
{
    public static readonly string Title = Helper.GetAppTitle() ?? "BotAgent";
    public static readonly string Description = Helper.GetAppDescription() ?? "Bot Agent";
    public static readonly Version? Version = Helper.GetAppVersion();
    //public static readonly string BaseDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory.Replace(@"\", "/"));
    public static readonly string BaseDir = Path.TrimEndingDirectorySeparator(@"C:\Jenkins\bot".Replace(@"\", "/"));
    public static readonly ManualResetEvent Mre = new(false);
    private static readonly Mutex mutex = new(true, Title);
    private readonly IHost host;
    private readonly AppTray tray;
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
        builder.Services.AddSingleton<ScreenSaver>();
        builder.Services.AddSingleton<Jenkins>();
        builder.Services.AddSingleton<Agent>();
        builder.Services.AddSingleton<AppTray>();
        host = builder.Build();
        agent = host.Services.GetRequiredService<Agent>();
        tray = host.Services.GetRequiredService<AppTray>();
    }

    public override void Initialize()
    {
        agent.Initialize();
        tray.Initialize();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(agent, host.Services.GetRequiredService<Jenkins>())
            };
            tray.RegisterMainWindow((MainWindowViewModel)app.MainWindow.DataContext);
        }
        base.OnFrameworkInitializationCompleted();
        Mre.Set();
    }

    public static void RunOnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action.Invoke();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    public static void Exit()
    {
        RunOnUIThread(() => {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
            {
                app.Shutdown();
            }
        });
    }

    private void SingleInstance()
    {
        if (!mutex.WaitOne(0, false))
        {
            mutex.Dispose();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
            {
                app.Shutdown();
            }
            Environment.Exit(0);
        }
    }
}