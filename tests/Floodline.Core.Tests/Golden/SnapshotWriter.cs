using System;
using System.Collections.Generic;
using System.Text;
using Floodline.Core;
using Floodline.Core.Movement;

namespace Floodline.Core.Tests.Golden;

internal static class SnapshotWriter
{
    private const string SnapshotVersion = "0.1";
    private const string CellOrder = "x,y,z";
    private const char LineBreak = '\n';

    public static string Write(
        Grid grid,
        GravityDirection gravity,
        SimulationState? state = null,
        ObjectiveEvaluation? objectives = null,
        ActivePiece? activePiece = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        SimulationState resolvedState = state ?? new SimulationState(SimulationStatus.InProgress, 0, 0);
        ObjectiveEvaluation resolvedObjectives = objectives ?? ObjectiveEvaluation.Empty;

        StringBuilder builder = new();
        AppendLine(builder, $"# FloodlineSnapshot v{SnapshotVersion}");
        AppendLine(builder, $"cellOrder: {CellOrder}");
        AppendLine(builder, $"bounds: {grid.Size.X} {grid.Size.Y} {grid.Size.Z}");
        AppendLine(builder, $"gravity: {gravity}");
        AppendLine(builder, $"status: {resolvedState.Status}");
        AppendLine(builder, $"ticks: {resolvedState.TicksElapsed}");
        AppendLine(builder, $"piecesLocked: {resolvedState.PiecesLocked}");
        AppendLine(builder, $"objectives: {resolvedObjectives.Objectives.Count} allCompleted={FormatBool(resolvedObjectives.AllCompleted)}");

        foreach (ObjectiveProgress progress in resolvedObjectives.Objectives)
        {
            string type = string.IsNullOrWhiteSpace(progress.Type) ? "UNKNOWN" : progress.Type.Trim();
            AppendLine(builder, $"objective: type={type} current={progress.Current} target={progress.Target} completed={FormatBool(progress.Completed)}");
        }

        if (activePiece is null)
        {
            AppendLine(builder, "activePiece: none");
        }
        else
        {
            OrientedPiece piece = activePiece.Piece;
            AppendLine(
                builder,
                $"activePiece: id={piece.Id} orientation={piece.OrientationIndex} origin={FormatInt3(activePiece.Origin)}");
        }

        List<CellSnapshot> cells = CollectCells(grid);
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

    private static string FormatMaterial(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
        {
            return "none";
        }

        return materialId.Trim().ToUpperInvariant();
    }

    private static string FormatBool(bool value) => value ? "true" : "false";

    private static string FormatInt3(Int3 pos) => $"{pos.X},{pos.Y},{pos.Z}";

    private static void AppendLine(StringBuilder builder, string line) => builder.Append(line).Append(LineBreak);

    private readonly record struct CellSnapshot(Int3 Position, OccupancyType Type, string Material);
}
