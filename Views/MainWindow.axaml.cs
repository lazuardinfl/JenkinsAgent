using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace Bot.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeEvent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeEvent()
    {
        Opened += OnStartup;
        Closing += OnWindowClosing;
    }

    // hide window on startup
    private void OnStartup(object? sender, EventArgs e)
    {
        Hide();
        Opened -= OnStartup;
    }

    // hide instead close window
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        Hide();
        e.Cancel = true;
    }
}