using System;
using System.Collections.Generic;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class FreezeDrainAbilityTests
{
    private static readonly Int3[] Adj6Offsets =
    [
        new Int3(1, 0, 0),
        new Int3(-1, 0, 0),
        new Int3(0, 1, 0),
        new Int3(0, -1, 0),
        new Int3(0, 0, 1),
        new Int3(0, 0, -1)
    ];

    private static readonly Int3[] HorizontalAdjOffsets =
    [
        new Int3(1, 0, 0),
        new Int3(-1, 0, 0),
        new Int3(0, 0, 1),
        new Int3(0, 0, -1)
    ];

    private static readonly Int3[] DiagonalOffsets =
    [
        new Int3(1, 1, 1),
        new Int3(1, 1, -1),
        new Int3(1, -1, 1),
        new Int3(1, -1, -1),
        new Int3(-1, 1, 1),
        new Int3(-1, 1, -1),
        new Int3(-1, -1, 1),
        new Int3(-1, -1, -1)
    ];

    [Fact]
    public void FreezeAbility_Adj6_FreezesAdjacent_And_SkipsDiagonal()
    {
        Level level = CreateLevel(new AbilitiesConfig(
            FreezeCharges: 1,
            FreezeDurationResolves: 2,
            FreezeScope: FreezeScope.Adj6));

        Simulation sim = new(level, new Pcg32(1));
        ActivePiece piece = sim.ActivePiece!;

        Int3 adjacent = FindLandingOffsetCell(piece, sim.Grid, Adj6Offsets);
        Int3 diagonal = FindLandingOffsetCell(piece, sim.Grid, DiagonalOffsets);

        sim.Grid.SetVoxel(adjacent, Voxel.Water);
        sim.Grid.SetVoxel(diagonal, Voxel.Water);

        sim.Tick(InputCommand.FreezeAbility);
        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(OccupancyType.Ice, sim.Grid.GetVoxel(adjacent).Type);
        Assert.NotEqual(OccupancyType.Ice, sim.Grid.GetVoxel(diagonal).Type);
    }

    [Fact]
    public void FreezeAbility_Disarm_Prevents_Freeze()
    {
        Level level = CreateLevel(new AbilitiesConfig(
            FreezeCharges: 1,
            FreezeDurationResolves: 2,
            FreezeScope: FreezeScope.Adj6));

        Simulation sim = new(level, new Pcg32(2));
        ActivePiece piece = sim.ActivePiece!;
        Int3 adjacent = FindLandingOffsetCell(piece, sim.Grid, Adj6Offsets);
        sim.Grid.SetVoxel(adjacent, Voxel.Water);

        sim.Tick(InputCommand.FreezeAbility);
        sim.Tick(InputCommand.FreezeAbility);
        sim.Tick(InputCommand.HardDrop);

        Assert.NotEqual(OccupancyType.Ice, sim.Grid.GetVoxel(adjacent).Type);
    }

    [Fact]
    public void FreezeAbility_Adj26_Freezes_Diagonal()
    {
        Level level = CreateLevel(new AbilitiesConfig(
            FreezeCharges: 1,
            FreezeDurationResolves: 2,
            FreezeScope: FreezeScope.Adj26));

        Simulation sim = new(level, new Pcg32(3));
        ActivePiece piece = sim.ActivePiece!;
        Int3 diagonal = FindLandingOffsetCell(piece, sim.Grid, DiagonalOffsets);
        sim.Grid.SetVoxel(diagonal, Voxel.Water);

        sim.Tick(InputCommand.FreezeAbility);
        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(OccupancyType.Ice, sim.Grid.GetVoxel(diagonal).Type);
    }

    [Fact]
    public void DrainPlacement_PlacesDrain_On_PivotColumn()
    {
        Level level = CreateLevel(new AbilitiesConfig(
            DrainPlacementCharges: 1,
            DrainPlacement: new DrainConfig(1, DrainScope.Adj6)));

        Simulation sim = new(level, new Pcg32(4));
        Int3 origin = sim.ActivePiece!.Origin;

        sim.Tick(InputCommand.DrainPlacementAbility);
        sim.Tick(InputCommand.HardDrop);

        Int3 drainPos = FindSingleDrain(sim.Grid);
        Assert.Equal(origin.X, drainPos.X);
        Assert.Equal(origin.Z, drainPos.Z);
        Assert.Equal(OccupancyType.Drain, sim.Grid.GetVoxel(drainPos).Type);
    }

    [Fact]
    public void DrainPlacement_Disarm_Prevents_Placement()
    {
        Level level = CreateLevel(new AbilitiesConfig(
            DrainPlacementCharges: 1,
            DrainPlacement: new DrainConfig(1, DrainScope.Adj6)));

        Simulation sim = new(level, new Pcg32(5));

        sim.Tick(InputCommand.DrainPlacementAbility);
        sim.Tick(InputCommand.DrainPlacementAbility);
        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(0, CountDrains(sim.Grid));
    }

    [Fact]
    public void DrainPlacement_Config_Removes_Adjacent_Water()
    {
        Level level = CreateLevel(new AbilitiesConfig(
            DrainPlacementCharges: 1,
            DrainPlacement: new DrainConfig(1, DrainScope.Adj6)));

        Simulation sim = new(level, new Pcg32(6));
        ActivePiece piece = sim.ActivePiece!;
        Int3 offset = FindHorizontalOffset(piece);
        Int3 landingOrigin = GetLandingOrigin(piece);
        Int3 waterPos = landingOrigin + offset;

        Assert.True(sim.Grid.IsInBounds(waterPos));
        sim.Grid.SetVoxel(waterPos, Voxel.Water);

        sim.Tick(InputCommand.DrainPlacementAbility);
        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(waterPos).Type);
    }

    private static Level CreateLevel(AbilitiesConfig abilities) =>
        new(
            new LevelMeta("test_id", "Test Title", "0.2.2", 12345U),
            new Int3(10, 20, 10),
            [],
            [],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", ["I4"], null),
            [],
            abilities);

    private static Int3 GetLandingOrigin(ActivePiece piece)
    {
        int minYOffset = GetMinOffsetY(piece);
        return new Int3(piece.Origin.X, -minYOffset, piece.Origin.Z);
    }

    private static int GetMinOffsetY(ActivePiece piece)
    {
        int min = int.MaxValue;
        foreach (Int3 offset in piece.Piece.Voxels)
        {
            if (offset.Y < min)
            {
                min = offset.Y;
            }
        }

        return min;
    }

    private static Int3 FindLandingOffsetCell(ActivePiece piece, Grid grid, IReadOnlyList<Int3> offsets)
    {
        HashSet<Int3> occupiedOffsets = [.. piece.Piece.Voxels];
        Int3 landingOrigin = GetLandingOrigin(piece);
        foreach (Int3 voxelOffset in occupiedOffsets)
        {
            Int3 pos = landingOrigin + voxelOffset;
            foreach (Int3 offset in offsets)
            {
                Int3 candidate = pos + offset;
                if (grid.IsInBounds(candidate) && !occupiedOffsets.Contains(candidate - landingOrigin))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("No candidate offset cell found for test.");
    }

    private static Int3 FindHorizontalOffset(ActivePiece piece)
    {
        HashSet<Int3> occupiedOffsets = [.. piece.Piece.Voxels];
        foreach (Int3 offset in HorizontalAdjOffsets)
        {
            if (!occupiedOffsets.Contains(offset))
            {
                return offset;
            }
        }

        throw new InvalidOperationException("No horizontal offset available for drain test.");
    }

    private static Int3 FindSingleDrain(Grid grid)
    {
        List<Int3> drains = [];
        for (int x = 0; x < grid.Size.X; x++)
        {
            for (int y = 0; y < grid.Size.Y; y++)
            {
                for (int z = 0; z < grid.Size.Z; z++)
                {
                    Int3 pos = new(x, y, z);
                    if (grid.GetVoxel(pos).Type == OccupancyType.Drain)
                    {
                        drains.Add(pos);
                    }
                }
            }
        }

        if (drains.Count != 1)
        {
            throw new InvalidOperationException($"Expected one drain but found {drains.Count}.");
        }

        return drains[0];
    }

    private static int CountDrains(Grid grid)
    {
        int count = 0;
        for (int x = 0; x < grid.Size.X; x++)
        {
            for (int y = 0; y < grid.Size.Y; y++)
            {
                for (int z = 0; z < grid.Size.Z; z++)
                {
                    if (grid.GetVoxel(new Int3(x, y, z)).Type == OccupancyType.Drain)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }
}
