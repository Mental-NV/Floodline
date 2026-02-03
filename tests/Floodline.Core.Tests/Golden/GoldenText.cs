using System;

namespace Floodline.Core.Tests.Golden;

internal static class GoldenText
{
    public static string Normalize(string? text)
    {
        if (text is null)
        {
            return string.Empty;
        }

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return normalized.TrimEnd('\n');
    }

    public static string[] SplitLines(string? text)
    {
        string normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return [];
        }

        return normalized.Split('\n');
    }
}
