using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bot.Helpers;

public static class MessageBoxHelper
{
    public static DialogResult ShowError(string msg)
        => MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static async void ShowErrorFireForget(string msg)
        => await ShowAsync("Error", msg, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static Task<DialogResult> ShowErrorAsync(string msg)
        => ShowAsync("Error", msg, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static Task<DialogResult> ShowQuestionYesNoAsync(string title, string msg)
        => ShowAsync(title, msg, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

    public static Task<DialogResult> ShowQuestionOkCancelAsync(string title, string msg)
        => ShowAsync(title, msg, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

    public static Task<DialogResult> ShowAsync(string title, string msg, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Task.Run(() => MessageBox.Show(msg, title, buttons, icon));
}