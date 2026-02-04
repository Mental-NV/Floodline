using System.Collections.Generic;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class WindHazardTests
{
    [Fact]
    public void WindHazard_Fires_On_Schedule()
    {
        HazardConfig hazard = CreateWindHazard(
            intervalTicks: 2,
            pushStrength: 1,
            directionMode: "FIXED",
            firstOffset: 0,
            fixedDirection: "EAST");

        Simulation sim = new(CreateLevel(hazard), new Pcg32(1));
        int startX = sim.ActivePiece!.Origin.X;

        sim.Tick(InputCommand.None);
        Assert.Equal(startX + 1, sim.ActivePiece!.Origin.X);

        sim.Tick(InputCommand.None);
        Assert.Equal(startX + 1, sim.ActivePiece!.Origin.X);

        sim.Tick(InputCommand.None);
        Assert.Equal(startX + 2, sim.ActivePiece!.Origin.X);
    }

    [Fact]
    public void WindHazard_AlternateEw_Switches_Direction()
    {
        HazardConfig hazard = CreateWindHazard(
            intervalTicks: 1,
            pushStrength: 1,
            directionMode: "ALTERNATE_EW",
            firstOffset: 0);

        Simulation sim = new(CreateLevel(hazard), new Pcg32(2));
        int startX = sim.ActivePiece!.Origin.X;

        sim.Tick(InputCommand.None);
        Assert.Equal(startX + 1, sim.ActivePiece!.Origin.X);

        sim.Tick(InputCommand.None);
        Assert.Equal(startX, sim.ActivePiece!.Origin.X);
    }

    [Fact]
    public void WindHazard_FixedDirection_UsesSpecifiedAxis()
    {
        HazardConfig hazard = CreateWindHazard(
            intervalTicks: 1,
            pushStrength: 1,
            directionMode: "FIXED",
            firstOffset: 0,
            fixedDirection: "NORTH");

        Simulation sim = new(CreateLevel(hazard), new Pcg32(3));
        int startZ = sim.ActivePiece!.Origin.Z;

        sim.Tick(InputCommand.None);
        Assert.Equal(startZ - 1, sim.ActivePiece!.Origin.Z);
    }

    [Fact]
    public void WindHazard_Stops_On_Collision()
    {
        HazardConfig hazard = CreateWindHazard(
            intervalTicks: 1,
            pushStrength: 3,
            directionMode: "FIXED",
            firstOffset: 0,
            fixedDirection: "EAST");

        Simulation sim = new(CreateLevel(hazard), new Pcg32(4));
        Int3 origin = sim.ActivePiece!.Origin;
        Int3 blocker = GetDestinationCell(sim.ActivePiece!, new Int3(1, 0, 0), sim.Grid);
        sim.Grid.SetVoxel(blocker, new Voxel(OccupancyType.Solid, "STANDARD"));

        sim.Tick(InputCommand.None);

        Assert.Equal(origin, sim.ActivePiece!.Origin);
    }

    [Fact]
    public void WindHazard_Passes_Through_Water()
    {
        HazardConfig hazard = CreateWindHazard(
            intervalTicks: 1,
            pushStrength: 1,
            directionMode: "FIXED",
            firstOffset: 0,
            fixedDirection: "EAST");

        Simulation sim = new(CreateLevel(hazard), new Pcg32(5));
        Int3 origin = sim.ActivePiece!.Origin;
        Int3 waterCell = GetDestinationCell(sim.ActivePiece!, new Int3(1, 0, 0), sim.Grid);
        sim.Grid.SetVoxel(waterCell, Voxel.Water);

        sim.Tick(InputCommand.None);

        Assert.Equal(origin.X + 1, sim.ActivePiece!.Origin.X);
        Assert.Equal(OccupancyType.Water, sim.Grid.GetVoxel(waterCell).Type);
    }

    [Fact]
    public void WindHazard_HeavyPieces_Reduce_Push()
    {
        HazardConfig hazard = CreateWindHazard(
            intervalTicks: 1,
            pushStrength: 1,
            directionMode: "FIXED",
            firstOffset: 0,
            fixedDirection: "EAST");

        Simulation sim = new(CreateLevel(hazard, bagToken: "I4:HEAVY"), new Pcg32(6));
        Int3 origin = sim.ActivePiece!.Origin;

        sim.Tick(InputCommand.None);

        Assert.Equal(origin, sim.ActivePiece!.Origin);
    }

    private static Level CreateLevel(HazardConfig hazard, string bagToken = "I4") =>
        new(
            new LevelMeta("wind_test", "Wind Test", "0.2.0", 123U),
            new Int3(10, 20, 10),
            [],
            [],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", [bagToken], null),
            [hazard]);

    private static HazardConfig CreateWindHazard(
        int intervalTicks,
        int pushStrength,
        string directionMode,
        int? firstOffset,
        string? fixedDirection = null)
    {
        Dictionary<string, object> parameters = new()
        {
            ["intervalTicks"] = intervalTicks,
            ["pushStrength"] = pushStrength,
            ["directionMode"] = directionMode
        };

        if (firstOffset.HasValue)
        {
            parameters["firstGustOffsetTicks"] = firstOffset.Value;
        }

        if (!string.IsNullOrWhiteSpace(fixedDirection))
        {
            parameters["fixedDirection"] = fixedDirection;
        }

        return new HazardConfig("WIND_GUST", true, parameters);
    }

    private static Int3 GetDestinationCell(ActivePiece piece, Int3 direction, Grid grid)
    {
        foreach (Int3 pos in piece.GetWorldPositions())
        {
            Int3 candidate = pos + direction;
            if (grid.IsInBounds(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No in-bounds destination cell found for wind test.");
    }
}
