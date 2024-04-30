using Microsoft.Win32.TaskScheduler;
using System;

namespace Bot.Helpers;

public static class TaskSchedulerHelper
{
    public static Task GetTask(string name) => TaskService.Instance.GetTask(name.Replace("/", @"\"));

    public static bool? GetStatus(string? name) => GetTask(name ?? "")?.Enabled;

    public static bool Enable(string? name, bool enabled)
    {
        try
        {
            GetTask(name ?? "").Enabled = enabled;
            return true;
        }
        catch (Exception) { return false; }
    }

    public static bool Create(string? name, string appName, string workingDirectory, bool enabled = false) 
    {
        try
        {
            using (TaskDefinition definition = TaskService.Instance.NewTask())
            {
                definition.Actions.Add("cmd", $"/c start \"\" \"{appName}\" startup", workingDirectory.Replace("/", @"\"));
                definition.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromMinutes(1) });
                definition.Principal.RunLevel = TaskRunLevel.Highest;
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.Enabled = enabled;
                TaskService.Instance.RootFolder.RegisterTaskDefinition(name?.Replace("/", @"\") ?? " ", definition);
            }
            return true;
        }
        catch (Exception) { return false; }
    }
}