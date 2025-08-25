using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace Bot.Models;

public enum BotIcon { Normal, Offline }

public partial class TrayMenu : ObservableObject
{
    private readonly Action action;

    private readonly Func<Task> func;

    [ObservableProperty]
    private string header;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private bool isChecked;

    public TrayMenu(string header, bool isVisible, bool isEnabled, bool isChecked, Func<Task> func, Action action)
    {
        (this.header, this.func, this.action) = (header, func, action);
        (this.isVisible, this.isEnabled, this.isChecked) = (isVisible, isEnabled, isChecked);
    }

    public TrayMenu(string header, Func<Task> func, bool isVisible = true, bool isEnabled = true, bool isChecked = false)
        : this(header, isVisible, isEnabled, isChecked, func, () => { })
    { }

    public TrayMenu(string header, Action action, bool isVisible = true, bool isEnabled = true, bool isChecked = false)
        : this(header, isVisible, isEnabled, isChecked, () => Task.Delay(1), action)
    { }

    public TrayMenu(string header, bool isVisible = true, bool isEnabled = true, bool isChecked = false)
        : this(header, isVisible, isEnabled, isChecked, () => Task.Delay(1), () => { })
    { }

    [RelayCommand(CanExecute = nameof(IsEnabled))]
    private void Invoke() => action();

    [RelayCommand(CanExecute = nameof(IsEnabled))]
    private async Task AsyncInvoke() => await func();
}
