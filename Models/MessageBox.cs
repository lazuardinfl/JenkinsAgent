using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace Bot.Models;

public static class MessageBox
{
    public static IMsBox<ButtonResult> InvalidJenkinsCredential()
    {
        return MessageBoxManager.GetMessageBoxStandard("Error", "Bot config invalid.\nPlease input correct bot config.", ButtonEnum.Ok, Icon.Error);
    }
}