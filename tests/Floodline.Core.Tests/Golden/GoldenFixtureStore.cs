using System;
using System.IO;

namespace Floodline.Core.Tests.Golden;

internal static class GoldenFixtureStore
{
    private const string Extension = ".snap";
    private static readonly string BaseDirectory = Path.Combine(AppContext.BaseDirectory, "fixtures", "golden");

    public static string Load(string name)
    {
        string path = GetPath(name);
        return File.ReadAllText(path);
    }

    public static void Save(string name, string content)
    {
        string path = GetPath(name);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }

    public static string GetPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Golden fixture name must be provided.", nameof(name));
        }

        string relative = name.Replace('/', Path.DirectorySeparatorChar);
        if (!relative.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            relative += Extension;
        }

        return Path.Combine(BaseDirectory, relative);
    }
}
