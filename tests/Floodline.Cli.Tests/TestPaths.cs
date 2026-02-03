using System;
using System.IO;

namespace Floodline.Cli.Tests;

internal static class TestPaths
{
    private static string RepoRoot { get; } = FindRepoRoot();

    public static string GetLevelPath(string fileName) => Path.Combine(RepoRoot, "levels", fileName);

    public static string GetCliFixturePath(string fileName) =>
        Path.Combine(RepoRoot, "tests", "Floodline.Cli.Tests", "fixtures", fileName);

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Floodline.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root containing Floodline.sln.");
    }
}
