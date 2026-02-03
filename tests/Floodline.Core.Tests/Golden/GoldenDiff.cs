using System;
using System.Globalization;
using System.Text;

namespace Floodline.Core.Tests.Golden;

internal static class GoldenDiff
{
    public static string Build(string expected, string actual)
    {
        string[] expectedLines = GoldenText.SplitLines(expected);
        string[] actualLines = GoldenText.SplitLines(actual);

        int maxLines = Math.Max(expectedLines.Length, actualLines.Length);
        for (int index = 0; index < maxLines; index++)
        {
            string expectedLine = index < expectedLines.Length ? expectedLines[index] : "<missing>";
            string actualLine = index < actualLines.Length ? actualLines[index] : "<missing>";

            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                return FormatMismatch(index + 1, expectedLine, actualLine, expectedLines.Length, actualLines.Length);
            }
        }

        return "No differences found.";
    }

    private static string FormatMismatch(
        int lineNumber,
        string expectedLine,
        string actualLine,
        int expectedCount,
        int actualCount)
    {
        StringBuilder builder = new();
        builder.Append("First difference at line ").Append(lineNumber).AppendLine(":");
        builder.Append("Expected: ").AppendLine(expectedLine);
        builder.Append("Actual:   ").AppendLine(actualLine);
        builder.Append("Expected lines: ").AppendLine(expectedCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("Actual lines:   ").AppendLine(actualCount.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
