using System;
using System.IO;
using Xunit.Sdk;

namespace Floodline.Core.Tests.Golden;

internal static class GoldenAssert
{
    private const string UpdateEnvVar = "FLOODLINE_UPDATE_GOLDENS";

    public static void Matches(string name, string actual)
    {
        string normalizedActual = GoldenText.Normalize(actual);
        string path = GoldenFixtureStore.GetPath(name);

        if (ShouldUpdateGoldens())
        {
            GoldenFixtureStore.Save(name, normalizedActual + "\n");
            return;
        }

        if (!File.Exists(path))
        {
            throw new XunitException($"Golden fixture not found: {path}. Set {UpdateEnvVar}=1 to write fixtures.");
        }

        string expected = GoldenText.Normalize(GoldenFixtureStore.Load(name));
        if (!string.Equals(expected, normalizedActual, StringComparison.Ordinal))
        {
            string diff = GoldenDiff.Build(expected, normalizedActual);
            throw new XunitException($"Golden snapshot mismatch for '{name}'.\n{diff}");
        }
    }

    private static bool ShouldUpdateGoldens()
    {
        string? value = Environment.GetEnvironmentVariable(UpdateEnvVar);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
