using System;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bot.Helpers;

public static partial class Helper
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string? GetAppTitle() => Assembly.GetExecutingAssembly().GetName().Name;

    public static string? GetAppDescription() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

    public static Version? GetAppVersion() => Assembly.GetExecutingAssembly().GetName().Version;

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();
    public static string RemoveWhitespaces(string source)
    {
        return WhitespaceRegex().Replace(source, string.Empty);
    }
}