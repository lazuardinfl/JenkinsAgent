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

    public static string? CreateUrl(string? url1, string? url2 = null)
    {
        string? url = "";
        try
        {
            switch (url1?.Contains("://"))
            {
                case true:
                    url = url1.TrimEnd('/');
                    break;
                default:
                    url = (new UriBuilder(url1!) { Scheme = Uri.UriSchemeHttps, Port = -1 }).Uri.AbsoluteUri.TrimEnd('/');
                    break;
            }
            switch ((bool?)(url2 == null ? null : Uri.IsWellFormedUriString(url2, UriKind.Absolute)))
            {
                case true:
                    url = url2;
                    break;
                case false:
                    url = $"{url}/{url2}";
                    break;
            }
            return url;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();
    public static string RemoveWhitespaces(string text) => WhitespaceRegex().Replace(text, string.Empty);
}