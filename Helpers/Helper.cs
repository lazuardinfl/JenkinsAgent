using System;
using System.IO;
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

    public static string GetBaseDir() => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    public static string GetUserDir() => Environment.GetEnvironmentVariable("USERPROFILE")!;

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();
    public static string RemoveWhitespaces(string text) => WhitespaceRegex().Replace(text, string.Empty);
}