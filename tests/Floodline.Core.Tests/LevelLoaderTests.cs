#pragma warning disable JSON002

using Floodline.Core.Levels;

namespace Floodline.Core.Tests;

public class LevelLoaderTests
{
    [Fact]
    public void LoadValidJsonReturnsLevel()
    {
        // Arrange
        string baseDir = AppContext.BaseDirectory;
        string path = Path.Combine(baseDir, "fixtures", "minimal_level.json");
        string json = File.ReadAllText(path);

        // Act
        Level level = LevelLoader.Load(json);

        // Assert
        Assert.NotNull(level);
        Assert.Equal("test-level", level.Meta.Id);
        Assert.Equal(10, level.Bounds.X);
        _ = Assert.Single(level.InitialVoxels);
        Assert.Equal(OccupancyType.Bedrock, level.InitialVoxels[0].Type);
    }

    [Fact]
    public void LoadFloatingPointDurationThrowsArgumentException()
    {
        // Arrange
        string json = @"
{
  ""meta"": { ""id"": ""fail"", ""title"": ""fail"", ""schemaVersion"": ""0.2.0"", ""seed"": 1 },
  ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 },
  ""initialVoxels"": [],
  ""objectives"": [],
  ""rotation"": { ""cooldownTicks"": 60.5 },
  ""bag"": { ""type"": ""Fixed"" },
  ""hazards"": []
}";

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LevelLoader.Load(json));
        Assert.Contains("Floating point number found", ex.Message);
    }

    [Fact]
    public void LoadEmptyJsonThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(() => LevelLoader.Load(""));

    [Fact]
    public void LoadMissingMetaThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(() => LevelLoader.Load(@"{ ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 } }"));

    [Fact]
    public void LoadMissingRotationThrowsArgumentException()
    {
        string json = @"{
            ""meta"": { ""id"": ""fail"", ""title"": ""fail"", ""schemaVersion"": ""0.2.0"", ""seed"": 1 },
            ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 },
            ""initialVoxels"": [],
            ""objectives"": [],
            ""bag"": { ""type"": ""Fixed"" },
            ""hazards"": []
        }";
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LevelLoader.Load(json));
        Assert.Contains("Rotation configuration is missing", ex.Message);
    }

    [Fact]
    public void LoadMissingInitialVoxelsThrowsArgumentException()
    {
        string json = @"{
            ""meta"": { ""id"": ""fail"", ""title"": ""fail"", ""schemaVersion"": ""0.2.0"", ""seed"": 1 },
            ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 },
            ""objectives"": [],
            ""rotation"": { ""cooldownTicks"": 60 },
            ""bag"": { ""type"": ""Fixed"" },
            ""hazards"": []
        }";
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LevelLoader.Load(json));
        Assert.Contains("InitialVoxels list is missing", ex.Message);
    }

    [Fact]
    public void LoadMissingBagTypeThrowsArgumentException()
    {
        string json = @"{
            ""meta"": { ""id"": ""fail"", ""title"": ""fail"", ""schemaVersion"": ""0.2.0"", ""seed"": 1 },
            ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 },
            ""initialVoxels"": [],
            ""objectives"": [],
            ""rotation"": { ""cooldownTicks"": 60 },
            ""bag"": { },
            ""hazards"": []
        }";
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LevelLoader.Load(json));
        Assert.Contains("Bag type is missing", ex.Message);
    }

    [Fact]
    public void LoadMissingObjectiveTypeThrowsArgumentException()
    {
        string json = @"{
            ""meta"": { ""id"": ""fail"", ""title"": ""fail"", ""schemaVersion"": ""0.2.0"", ""seed"": 1 },
            ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 },
            ""initialVoxels"": [],
            ""objectives"": [{ ""params"": {} }],
            ""rotation"": { ""cooldownTicks"": 60 },
            ""bag"": { ""type"": ""Fixed"" },
            ""hazards"": []
        }";
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LevelLoader.Load(json));
        Assert.Contains("Objective type is missing", ex.Message);
    }

    [Fact]
    public void LoadMissingHazardTypeThrowsArgumentException()
    {
        string json = @"{
            ""meta"": { ""id"": ""fail"", ""title"": ""fail"", ""schemaVersion"": ""0.2.0"", ""seed"": 1 },
            ""bounds"": { ""x"": 10, ""y"": 10, ""z"": 10 },
            ""initialVoxels"": [],
            ""objectives"": [],
            ""rotation"": { ""cooldownTicks"": 60 },
            ""bag"": { ""type"": ""Fixed"" },
            ""hazards"": [{ ""params"": {} }]
        }";
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LevelLoader.Load(json));
        Assert.Contains("Hazard type is missing", ex.Message);
    }
}


