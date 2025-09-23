using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Bot.Helpers;

public enum MessageStatus { ConnectionFailed, VersionIncompatible, UnexpectedError }

public enum MessageBoxResult { None, Ok, Cancel, Abort, Retry, Ignore, Yes, No }

public enum MessageBoxButtons : uint { Ok = 0x00000000, OkCancel = 0x00000001, YesNo = 0x00000004 }

public enum MessageBoxIcon : uint { Information = 0x00000040, Warning = 0x00000030, Question = 0x00000020, Error = 0x00000010 }

public static class MessageBoxHelper
{
    public static MessageBoxResult ShowError(string msg)
        => (MessageBoxResult)MessageBoxA(IntPtr.Zero, msg, "Error", (uint)MessageBoxButtons.Ok | (uint)MessageBoxIcon.Error);

    public static async void ShowErrorFireForget(string msg)
        => await ShowAsync("Error", msg, MessageBoxButtons.Ok, MessageBoxIcon.Error);

    public static Task<MessageBoxResult> ShowInformationAsync(string msg)
        => ShowAsync("Information", msg, MessageBoxButtons.Ok, MessageBoxIcon.Information);

    public static Task<MessageBoxResult> ShowWarningYesNoAsync(string title, string msg)
        => ShowAsync(title, msg, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

    public static Task<MessageBoxResult> ShowQuestionOkCancelAsync(string title, string msg)
        => ShowAsync(title, msg, MessageBoxButtons.OkCancel, MessageBoxIcon.Question);

    public static Task<MessageBoxResult> ShowAsync(string title, string msg, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Task.Run(() => (MessageBoxResult)MessageBoxA(IntPtr.Zero, msg, title, (uint)buttons | (uint)icon));

    public static string GetMessage(MessageStatus status) => status switch
    {
        MessageStatus.ConnectionFailed => "Connection failed. Make sure connected\nto server and bot config is valid!",
        MessageStatus.VersionIncompatible => "Application need update to latest version!",
        MessageStatus.UnexpectedError => "Unexpected error. Contact admin for help!",
        _ => ""
    };

    [DllImport("user32.dll")]
    private static extern int MessageBoxA(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
