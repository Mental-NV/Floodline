using System;
using System.Collections.Generic;
using Floodline.Core;
using Floodline.Core.Levels;
using Xunit;

namespace Floodline.Core.Tests.Golden;

public class ObjectiveGoldenTests
{
    [Fact]
    public void Golden_ReachHeightAndWeight()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(1, 0, 1), new Voxel(OccupancyType.Solid));
        grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid, "HEAVY"));

        List<ObjectiveConfig> objectives =
        [
            new ObjectiveConfig("REACH_HEIGHT", new Dictionary<string, object> { ["height"] = 2 }),
            new ObjectiveConfig("STAY_UNDER_WEIGHT", new Dictionary<string, object> { ["maxMass"] = 3 })
        ];

        Level level = CreateLevel(new Int3(3, 3, 3), objectives);
        ObjectiveEvaluation evaluation = ObjectiveEvaluator.Evaluate(grid, level, 0, 0, 0);

        GoldenAssert.Matches("objectives/reach_height_and_weight", SnapshotWriter.Write(grid, GravityDirection.Down, objectives: evaluation));
    }

    [Fact]
    public void Golden_DrainAndRotations()
    {
        Grid grid = new(new Int3(3, 3, 3));

        List<ObjectiveConfig> objectives =
        [
            new ObjectiveConfig("DRAIN_WATER", new Dictionary<string, object> { ["targetUnits"] = 5 }),
            new ObjectiveConfig("SURVIVE_ROTATIONS", new Dictionary<string, object> { ["rotations"] = 2 })
        ];

        Level level = CreateLevel(new Int3(3, 3, 3), objectives);
        ObjectiveEvaluation evaluation = ObjectiveEvaluator.Evaluate(grid, level, 0, 3, 2);

        GoldenAssert.Matches("objectives/drain_and_rotations", SnapshotWriter.Write(grid, GravityDirection.Down, objectives: evaluation));
    }

    [Fact]
    public void Evaluate_UnknownObjectiveType_Throws()
    {
        Grid grid = new(new Int3(1, 1, 1));
        List<ObjectiveConfig> objectives =
        [
            new ObjectiveConfig("UNKNOWN_OBJECTIVE", [])
        ];

        Level level = CreateLevel(new Int3(1, 1, 1), objectives);

        _ = Assert.Throws<ArgumentException>(() => ObjectiveEvaluator.Evaluate(grid, level, 0, 0, 0));
    }

    private static Level CreateLevel(Int3 bounds, List<ObjectiveConfig> objectives)
    {
        LevelMeta meta = new("test-level", "Test Level", "0.2.0", 1);
        return new Level(
            meta,
            bounds,
            [],
            objectives,
            new RotationConfig(),
            new BagConfig("Fixed"),
            []);
    }
}
