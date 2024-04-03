using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Bot.Helpers;

public static partial class Helper
{
    private static readonly string file = "D:\\error.txt";

    public static void LogToFile(string msg)
    {
        File.AppendAllText(file, $"\n{DateTime.Now} # {msg}");
    }

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();

    public static string RemoveWhitespaces(string source)
    {
        return WhitespaceRegex().Replace(source, string.Empty);
    }
}