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
}
