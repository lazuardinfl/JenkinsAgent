using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Bot.Helpers;
using Bot.Services;
using Bot.ViewModels;
using Bot.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;

namespace Bot;

public partial class App : Application
{
    public const string DefaultConfigUrl = "public/config/bot.json";
    public static readonly string Title = Helper.GetAppTitle() ?? "Bot";
    public static readonly string Description = Helper.GetAppDescription() ?? "Bot Agent";
    public static readonly Version? Version = Helper.GetAppVersion();
    public static readonly string Hash = Helper.GetAppHash();
    public static readonly bool IsElevated = Helper.IsAppElevated();
    public static readonly string BaseDir = Helper.GetBaseDir().Replace(@"\", "/");
    public static readonly string ProfileDir = $"{Helper.GetUserDir().Replace(@"\", "/")}/{Title}";
    private static readonly Mutex mutex = new(true, Title);
    private readonly IHost host;
    private readonly Agent agent;

    public App()
    {
        SingleInstance();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders()
            .AddEventLog()
            .AddEventSourceLogger()
            .AddConsole()
            .AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = false;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss K # ";
            });
        builder.Services.AddHttpClient()
            .AddSingleton<Config>()
            .AddSingleton<AutoStartup>()
            .AddSingleton<ScreenSaver>()
            .AddSingleton<Jenkins>()
            .AddSingleton<Agent>()
            .AddTransient<ApplicationViewModel>()
            .AddTransient<MainWindowViewModel>();
        host = builder.Build();
        agent = host.Services.GetRequiredService<Agent>();
        DataContext = host.Services.GetRequiredService<ApplicationViewModel>();
    }

    public override void Initialize()
    {
        agent.Initialize();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit
            foreach (var plugin in BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray())
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
            // Load main window
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow { DataContext = host.Services.GetRequiredService<MainWindowViewModel>() };
            if (desktop.MainWindow.DataContext is MainWindowViewModel mainWindow)
            {
                mainWindow.Initialize();
                if (DataContext is ApplicationViewModel app)
                {
                    app.ShowPage = mainWindow.Show;
                    app.Initialize();
                }
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static IClassicDesktopStyleApplicationLifetime Lifetime() => (IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!;

    private void SingleInstance()
    {
        if (!mutex.WaitOne(0, false))
        {
            MessageBoxHelper.ShowError("Application already running!");
            mutex.Dispose();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            Environment.Exit(0);
        }
    }
}
