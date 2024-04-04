using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace Bot.Models;

public static class MessageBox
{
    public static IMsBox<ButtonResult> Error(string msg) => Show("Error", msg, ButtonEnum.Ok, Icon.Error);

    public static IMsBox<ButtonResult> QuestionYesNo(string title, string msg) => Show(title, msg, ButtonEnum.YesNo, Icon.Question);

    public static IMsBox<ButtonResult> QuestionOkCancel(string title, string msg) => Show(title, msg, ButtonEnum.OkCancel, Icon.Question);

    public static IMsBox<ButtonResult> Show(string title, string msg, ButtonEnum button = ButtonEnum.Ok, Icon icon = Icon.None)
    {
        return MessageBoxManager.GetMessageBoxStandard(title, msg, button, icon);
    }
}