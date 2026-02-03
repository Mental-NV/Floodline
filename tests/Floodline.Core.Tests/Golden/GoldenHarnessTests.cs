using Floodline.Core;
using Floodline.Core.Movement;
using Xunit;

namespace Floodline.Core.Tests.Golden;

public class GoldenHarnessTests
{
    [Fact]
    public void SnapshotWriter_ProducesStableOutput()
    {
        Grid grid = new(new Int3(2, 2, 2));
        grid.SetVoxel(new Int3(0, 0, 0), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(1, 0, 0), Voxel.Water);
        grid.SetVoxel(new Int3(0, 1, 1), new Voxel(OccupancyType.Solid, "HEAVY"));

        SimulationState state = new(SimulationStatus.InProgress, 5, 1);
        ObjectiveProgress progress = new("DRAIN_WATER", 3, 10, false);
        ObjectiveEvaluation objectives = new([progress], false);

        PieceDefinition definition = PieceLibrary.Get(PieceId.O2);
        OrientedPiece oriented = new(PieceId.O2, definition.UniqueOrientations[0], 0);
        ActivePiece activePiece = new(oriented, new Int3(0, 1, 0));

        string snapshot = SnapshotWriter.Write(grid, GravityDirection.Down, state, objectives, activePiece);

        GoldenAssert.Matches("golden_harness", snapshot);
    }
}
