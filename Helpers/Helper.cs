using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bot.Helpers;

public static partial class Helper
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string? GetAppTitle() => Assembly.GetExecutingAssembly().GetName().Name;

    public static string? GetAppDescription() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

    public static Version? GetAppVersion() => Assembly.GetExecutingAssembly().GetName().Version;

    public static string GetAppHash()
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            using (FileStream file = File.OpenRead(Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location))
            {
                byte[] hash = sha256.ComputeHash(file);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }

    public static bool IsAppElevated()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static string GetBaseDir() => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    public static string GetUserDir() => Environment.GetEnvironmentVariable("USERPROFILE")!;

    public static TType? GetProperty<TType, TObject>(TObject obj, string name)
    {
        PropertyInfo? info = obj?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return (TType?)info?.GetValue(obj);
    }

    public static string? CreateUrl(string? url1, string? url2 = null, bool trim = true)
    {
        try
        {
            string? url = (url1?.Contains("://")) switch
            {
                true => url1,
                _ => new UriBuilder(url1!) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri,
            };
            switch ((bool?)(url2 == null ? null : Uri.IsWellFormedUriString(url2, UriKind.Absolute)))
            {
                case true:
                    url = url2;
                    break;
                case false:
                    url = $"{url.TrimEnd('/')}/{url2}";
                    break;
            }
            return trim ? url?.TrimEnd('/') : url;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string RemoveWhitespaces(string text) => WhitespaceRegex().Replace(text, string.Empty);

    [GeneratedRegex(@"\s")]
    public static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\<(.*?)\>")]
    public static partial Regex AngleBracketsRegex();
}