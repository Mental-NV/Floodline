using System.Collections.Generic;
using Floodline.Core;
using Floodline.Core.Levels;
using Xunit;

namespace Floodline.Core.Tests;

public class ObjectiveEvaluatorTests
{
    [Fact]
    public void ObjectiveEvaluator_ReachHeight_Completes_When_Target_Met()
    {
        // Arrange
        Level level = new(
            new LevelMeta("test_id", "Test", "0.2.0", 1),
            new Int3(4, 4, 4),
            [],
            [
                new("ReachHeight", new Dictionary<string, object>
                {
                    ["height"] = 2
                })
            ],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", ["I4"], null),
            []
        );

        Grid grid = new(new Int3(4, 4, 4));
        grid.SetVoxel(new Int3(0, 2, 0), new Voxel(OccupancyType.Solid));

        // Act
        ObjectiveEvaluation evaluation = ObjectiveEvaluator.Evaluate(grid, level, 1, 0, 0);

        // Assert
        Assert.True(evaluation.AllCompleted);
        Assert.Single(evaluation.Objectives);
        Assert.True(evaluation.Objectives[0].Completed);
        Assert.Equal(2, evaluation.Objectives[0].Current);
        Assert.Equal(2, evaluation.Objectives[0].Target);
    }
}
