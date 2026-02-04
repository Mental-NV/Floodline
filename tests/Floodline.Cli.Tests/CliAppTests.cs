using System;
using System.IO;
using Floodline.Cli;
using Floodline.Cli.Validation;
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

    [Theory]
    [InlineData("invalid_objective_unknown_type.json", "semantic.objective.type_invalid", "#/objectives/0/type")]
    [InlineData("invalid_hazard_direction_mode.json", "semantic.wind.direction_mode_invalid", "#/hazards/0/params/directionMode")]
    [InlineData("invalid_rotation_allowed_directions.json", "semantic.rotation.allowed_directions_empty", "#/rotation/allowedDirections")]
    [InlineData("invalid_bag_fixed_invalid_piece.json", "semantic.bag.token_invalid", "#/bag/sequence/0")]
    [InlineData("invalid_bag_weighted_empty_weights.json", "semantic.bag.weights_missing", "#/bag/weights")]
    public void LevelValidator_Returns_Semantic_Errors(string fixture, string ruleId, string jsonPointer)
    {
        string levelPath = TestPaths.GetCliFixturePath(fixture);

        LevelValidationResult result = LevelValidator.ValidateFile(levelPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.RuleId == ruleId && error.JsonPointer == jsonPointer);
    }

    [Theory]
    [InlineData("invalid_abilities_freeze_missing_duration.json", "schema.dependentRequired", "/abilities")]
    [InlineData("invalid_abilities_drain_missing_config.json", "schema.dependentRequired", "/abilities")]
    public void LevelValidator_Returns_Schema_Errors(string fixture, string ruleId, string jsonPointer)
    {
        string levelPath = TestPaths.GetCliFixturePath(fixture);

        LevelValidationResult result = LevelValidator.ValidateFile(levelPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.RuleId == ruleId && error.JsonPointer == jsonPointer);
    }

    [Fact]
    public void CampaignValidator_Returns_Error_For_Missing_Campaign_File()
    {
        string missingPath = TestPaths.GetLevelPath($"missing-campaign-{Guid.NewGuid()}.json");

        LevelValidationResult result = CampaignValidator.ValidateFile(missingPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.RuleId == "io.campaign_not_found" && error.FilePath == missingPath);
    }

    [Fact]
    public void CampaignValidator_Returns_Error_For_Missing_Level_File()
    {
        string campaignPath = TestPaths.GetCliFixturePath("invalid_campaign_missing_level.json");

        LevelValidationResult result = CampaignValidator.ValidateFile(campaignPath);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.RuleId == "campaign.level_not_found" &&
                     error.FilePath == campaignPath &&
                     error.JsonPointer == "#/levels/0/path");
    }

    [Fact]
    public void CampaignValidator_Returns_Level_Errors_For_Invalid_Level()
    {
        string campaignPath = TestPaths.GetCliFixturePath("invalid_campaign_invalid_level.json");
        string levelPath = TestPaths.GetCliFixturePath("invalid_level_missing_meta.json");

        LevelValidationResult result = CampaignValidator.ValidateFile(campaignPath);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => string.Equals(
                         Path.GetFullPath(error.FilePath),
                         Path.GetFullPath(levelPath),
                         StringComparison.OrdinalIgnoreCase) &&
                     error.RuleId.StartsWith("schema.", StringComparison.Ordinal));
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
