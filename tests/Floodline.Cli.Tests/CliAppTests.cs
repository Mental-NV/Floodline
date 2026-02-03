using System;
using System.IO;
using Floodline.Cli;
using Xunit;

namespace Floodline.Cli.Tests;

public class CliAppTests
{
    [Fact]
    public void CliApp_Help_Prints_Usage()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = CliApp.Run(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output.ToString());
        Assert.True(string.IsNullOrWhiteSpace(error.ToString()));
    }

    [Fact]
    public void CliApp_Runs_Level_With_Input_Script()
    {
        string levelPath = TestPaths.GetLevelPath("minimal_level.json");
        string inputsPath = TestPaths.GetLevelPath("minimal_inputs.txt");

        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = CliApp.Run(["--level", levelPath, "--inputs", inputsPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Status:", output.ToString());
        Assert.Contains("DeterminismHash:", output.ToString());
        Assert.True(string.IsNullOrWhiteSpace(error.ToString()));
    }

    [Fact]
    public void CliApp_Record_And_Replay_Matches_DeterminismHash()
    {
        string levelPath = TestPaths.GetLevelPath("minimal_level.json");
        string inputsPath = TestPaths.GetLevelPath("minimal_inputs.txt");
        string replayPath = Path.Combine(Path.GetTempPath(), $"floodline-replay-{Guid.NewGuid()}.json");

        try
        {
            using StringWriter outputRecord = new();
            using StringWriter errorRecord = new();

            int recordExit = CliApp.Run(
                ["--level", levelPath, "--inputs", inputsPath, "--record", replayPath],
                outputRecord,
                errorRecord);

            Assert.Equal(0, recordExit);
            Assert.True(File.Exists(replayPath));
            Assert.True(string.IsNullOrWhiteSpace(errorRecord.ToString()));

            string recordHash = ExtractHash(outputRecord.ToString());

            using StringWriter outputReplay = new();
            using StringWriter errorReplay = new();

            int replayExit = CliApp.Run(
                ["--level", levelPath, "--replay", replayPath],
                outputReplay,
                errorReplay);

            Assert.Equal(0, replayExit);
            Assert.True(string.IsNullOrWhiteSpace(errorReplay.ToString()));

            string replayHash = ExtractHash(outputReplay.ToString());
            Assert.Equal(recordHash, replayHash);
        }
        finally
        {
            if (File.Exists(replayPath))
            {
                File.Delete(replayPath);
            }
        }
    }

    [Fact]
    public void CliApp_Validate_Level_Succeeds()
    {
        string levelPath = TestPaths.GetLevelPath("minimal_level.json");

        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = CliApp.Run(["--level", levelPath, "--validate"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Validation OK", output.ToString());
        Assert.True(string.IsNullOrWhiteSpace(error.ToString()));
    }

    [Fact]
    public void CliApp_Validate_Level_Returns_Actionable_Errors()
    {
        string levelPath = TestPaths.GetCliFixturePath("invalid_level_missing_meta.json");

        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = CliApp.Run(["--level", levelPath, "--validate"], output, error);

        Assert.Equal(2, exitCode);
        string errorText = error.ToString();
        Assert.Contains(levelPath, errorText, StringComparison.Ordinal);
        Assert.Contains("#", errorText, StringComparison.Ordinal);
        Assert.Contains("[schema.", errorText, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(output.ToString()));
    }

    private static string ExtractHash(string output)
    {
        foreach (string line in output.Split(Environment.NewLine))
        {
            if (line.StartsWith("DeterminismHash:", StringComparison.Ordinal))
            {
                return line["DeterminismHash:".Length..].Trim();
            }
        }

        throw new InvalidOperationException("DeterminismHash not found in output.");
    }
}
