using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Bot.Helpers;

public static partial class Helper
{
    public static string? GetAppTitle()
    {
        return Assembly.GetExecutingAssembly().GetName().Name;
    }

    public static string? GetAppDescription()
    {
        return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
    }

    public static Version? GetAppVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version;
    }

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();
    public static string RemoveWhitespaces(string source)
    {
        return WhitespaceRegex().Replace(source, string.Empty);
    }
}