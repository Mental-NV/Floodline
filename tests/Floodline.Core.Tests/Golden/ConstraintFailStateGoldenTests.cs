using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests.Golden;

public class ConstraintFailStateGoldenTests
{
    private const string SnapshotVersion = "0.1";
    private const char LineBreak = '\n';

    [Fact]
    public void Golden_Constraint_Overflow()
    {
        ConstraintsConfig constraints = new(MaxWorldHeight: 2);
        Int3 bounds = new(6, 6, 6);
        Int3 spawn = GetSpawnOrigin(bounds);
        List<VoxelData> initial = [new VoxelData(spawn, OccupancyType.Bedrock)];
        Level level = CreateLevel(constraints, "O2:STANDARD", bounds, initial);
        Simulation sim = new(level, new Pcg32(1));

        string snapshot = WriteConstraintSnapshot("overflow", sim, constraints);

        GoldenAssert.Matches("constraints/overflow", snapshot);
    }

    [Fact]
    public void Golden_Constraint_Weight_Exceeded()
    {
        ConstraintsConfig constraints = new(MaxMass: 1);
        Simulation sim = new(CreateLevel(constraints, "I4:STANDARD"), new Pcg32(2));

        sim.Tick(InputCommand.HardDrop);

        string snapshot = WriteConstraintSnapshot("weight_exceeded", sim, constraints);
        GoldenAssert.Matches("constraints/weight_exceeded", snapshot);
    }

    [Fact]
    public void Golden_Constraint_Water_Forbidden()
    {
        ConstraintsConfig constraints = new(WaterForbiddenWorldHeightMin: 1);
        List<VoxelData> initial =
        [
            new VoxelData(new Int3(1, 0, 1), OccupancyType.Bedrock),
            new VoxelData(new Int3(0, 1, 1), OccupancyType.Bedrock),
            new VoxelData(new Int3(2, 1, 1), OccupancyType.Bedrock),
            new VoxelData(new Int3(1, 1, 0), OccupancyType.Bedrock),
            new VoxelData(new Int3(1, 1, 2), OccupancyType.Bedrock),
            new VoxelData(new Int3(1, 1, 1), OccupancyType.Water)
        ];
        Simulation sim = new(CreateLevel(constraints, "I4:STANDARD", null, initial), new Pcg32(3));

        sim.Tick(InputCommand.HardDrop);

        string snapshot = WriteConstraintSnapshot("water_forbidden", sim, constraints);
        GoldenAssert.Matches("constraints/water_forbidden", snapshot);
    }

    [Fact]
    public void Golden_Constraint_No_Resting_On_Water()
    {
        ConstraintsConfig constraints = new(NoRestingOnWater: true);
        Simulation sim = new(CreateLevel(constraints, "I4:STANDARD"), new Pcg32(4));

        sim.Grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid, "STANDARD", Anchored: true));
        sim.Grid.SetVoxel(new Int3(1, 0, 1), Voxel.Water);
        sim.Grid.SetVoxel(new Int3(0, 0, 1), new Voxel(OccupancyType.Bedrock));
        sim.Grid.SetVoxel(new Int3(2, 0, 1), new Voxel(OccupancyType.Bedrock));
        sim.Grid.SetVoxel(new Int3(1, 0, 0), new Voxel(OccupancyType.Bedrock));
        sim.Grid.SetVoxel(new Int3(1, 0, 2), new Voxel(OccupancyType.Bedrock));

        sim.Tick(InputCommand.HardDrop);

        string snapshot = WriteConstraintSnapshot("no_resting_on_water", sim, constraints);
        GoldenAssert.Matches("constraints/no_resting_on_water", snapshot);
    }

    [Fact]
    public void Golden_Constraint_Pass_Does_Not_Lose()
    {
        ConstraintsConfig constraints = new(MaxMass: 20, WaterForbiddenWorldHeightMin: 10, NoRestingOnWater: true);
        Simulation sim = new(CreateLevel(constraints, "O2:STANDARD"), new Pcg32(5));

        sim.Tick(InputCommand.HardDrop);

        string snapshot = WriteConstraintSnapshot("pass", sim, constraints);
        GoldenAssert.Matches("constraints/pass", snapshot);
    }

    private static Level CreateLevel(ConstraintsConfig constraints, string bagToken, Int3? bounds = null, List<VoxelData>? voxels = null) =>
        new(
            new LevelMeta("constraint-golden", "Constraint Golden", "0.2.0", 4242U),
            bounds ?? new Int3(10, 20, 10),
            voxels ?? [],
            [],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", [bagToken], null),
            [],
            Abilities: null,
            Constraints: constraints
        );

    private static Int3 GetSpawnOrigin(Int3 bounds) => new(bounds.X / 2, bounds.Y - 1, bounds.Z / 2);

    private static string WriteConstraintSnapshot(string scenario, Simulation sim, ConstraintsConfig constraints)
    {
        StringBuilder builder = new();
        AppendLine(builder, $"# FloodlineConstraintSnapshot v{SnapshotVersion}");
        AppendLine(builder, $"scenario: {scenario}");
        AppendLine(builder, $"constraints: {FormatConstraints(constraints)}");
        AppendLine(builder, $"gravity: {sim.Gravity}");
        AppendLine(builder, $"status: {sim.State.Status}");
        AppendLine(builder, $"ticks: {sim.State.TicksElapsed}");
        AppendLine(builder, $"piecesLocked: {sim.State.PiecesLocked}");
        AppendLine(builder, $"activePiece: {FormatActivePiece(sim.ActivePiece)}");

        List<CellSnapshot> cells = CollectCells(sim.Grid);
        AppendLine(builder, $"cells: {cells.Count}");
        foreach (CellSnapshot cell in cells)
        {
            AppendLine(builder, $"cell: {FormatInt3(cell.Position)} type={cell.Type} material={cell.Material}");
        }

        return builder.ToString();
    }

    private static List<CellSnapshot> CollectCells(Grid grid)
    {
        List<CellSnapshot> cells = [];
        for (int x = 0; x < grid.Size.X; x++)
        {
            for (int y = 0; y < grid.Size.Y; y++)
            {
                for (int z = 0; z < grid.Size.Z; z++)
                {
                    Int3 pos = new(x, y, z);
                    Voxel voxel = grid.GetVoxel(pos);
                    if (voxel.Type == OccupancyType.Empty)
                    {
                        continue;
                    }

                    cells.Add(new CellSnapshot(pos, voxel.Type, FormatMaterial(voxel.MaterialId)));
                }
            }
        }

        return cells;
    }

    private static string FormatConstraints(ConstraintsConfig constraints)
    {
        string maxWorldHeight = FormatOptionalInt(constraints.MaxWorldHeight);
        string maxMass = FormatOptionalInt(constraints.MaxMass);
        string waterMin = FormatOptionalInt(constraints.WaterForbiddenWorldHeightMin);
        string noResting = constraints.NoRestingOnWater ? "true" : "false";
        return $"maxWorldHeight={maxWorldHeight} maxMass={maxMass} waterForbiddenMin={waterMin} noRestingOnWater={noResting}";
    }

    private static string FormatOptionalInt(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "none";

    private static string FormatActivePiece(ActivePiece? piece)
    {
        if (piece is null)
        {
            return "none";
        }

        OrientedPiece oriented = piece.Piece;
        return $"id={oriented.Id} orientation={oriented.OrientationIndex} origin={FormatInt3(piece.Origin)}";
    }

    private static string FormatMaterial(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
        {
            return "none";
        }

        return materialId.Trim().ToUpperInvariant();
    }

    private static string FormatInt3(Int3 pos) => $"{pos.X},{pos.Y},{pos.Z}";

    private static void AppendLine(StringBuilder builder, string line) => builder.Append(line).Append(LineBreak);

    private readonly record struct CellSnapshot(Int3 Position, OccupancyType Type, string Material);
}
