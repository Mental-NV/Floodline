using System;
using System.Collections.Generic;
using System.Text.Json;
using Floodline.Core.Levels;

namespace Floodline.Core;

/// <summary>
/// Represents progress for a single objective.
/// </summary>
/// <param name="Type">Objective type identifier.</param>
/// <param name="Current">Current value.</param>
/// <param name="Target">Target value.</param>
/// <param name="Completed">Whether the objective is complete.</param>
public sealed record ObjectiveProgress(string Type, int Current, int Target, bool Completed);

/// <summary>
/// Represents the objective evaluation snapshot.
/// </summary>
/// <param name="Objectives">Objective progress list, in canonical order.</param>
/// <param name="AllCompleted">True if all objectives are complete.</param>
public sealed record ObjectiveEvaluation(IReadOnlyList<ObjectiveProgress> Objectives, bool AllCompleted)
{
    /// <summary>
    /// An empty objective evaluation.
    /// </summary>
    public static ObjectiveEvaluation Empty { get; } = new([], false);
}

/// <summary>
/// Evaluates objective progress based on the current grid state.
/// </summary>
public static class ObjectiveEvaluator
{
    private static readonly Int3[] PlateauOffsets =
    [
        new Int3(1, 0, 0),
        new Int3(-1, 0, 0),
        new Int3(0, 0, 1),
        new Int3(0, 0, -1)
    ];

    public static ObjectiveEvaluation Evaluate(
        Grid grid,
        Level level,
        int piecesLocked,
        int waterRemovedTotal,
        int rotationsExecuted)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        _ = piecesLocked;

        List<ObjectiveProgress> results = [];
        foreach (ObjectiveConfig objective in level.Objectives)
        {
            if (objective.Params is null)
            {
                throw new ArgumentException($"Objective '{objective.Type}' is missing params.");
            }

            string type = objective.Type ?? string.Empty;
            string normalized = NormalizeType(type);

            ObjectiveProgress progress = normalized switch
            {
                "DRAINWATER" => EvaluateDrainWater(type, objective.Params, waterRemovedTotal),
                "REACHHEIGHT" => EvaluateReachHeight(type, objective.Params, grid),
                "BUILDPLATEAU" => EvaluateBuildPlateau(type, objective.Params, grid),
                "STAYUNDERWEIGHT" => EvaluateStayUnderWeight(type, objective.Params, grid),
                "SURVIVEROTATIONS" => EvaluateSurviveRotations(type, objective.Params, rotationsExecuted),
                _ => throw new ArgumentException($"Unknown objective type '{objective.Type}'.")
            };

            results.Add(progress);
        }

        bool allCompleted = results.Count > 0 && results.TrueForAll(o => o.Completed);
        return new ObjectiveEvaluation(results, allCompleted);
    }

    private static ObjectiveProgress EvaluateDrainWater(
        string type,
        Dictionary<string, object> parameters,
        int waterRemovedTotal)
    {
        int target = GetRequiredInt(parameters, "targetUnits", "units", "target");
        bool completed = waterRemovedTotal >= target;
        return new ObjectiveProgress(type, waterRemovedTotal, target, completed);
    }

    private static ObjectiveProgress EvaluateReachHeight(
        string type,
        Dictionary<string, object> parameters,
        Grid grid)
    {
        int target = GetRequiredInt(parameters, "height", "worldHeight");
        int current = GetMaxWorldHeight(grid);
        bool completed = current >= target;
        return new ObjectiveProgress(type, current, target, completed);
    }

    private static ObjectiveProgress EvaluateBuildPlateau(
        string type,
        Dictionary<string, object> parameters,
        Grid grid)
    {
        int area = GetRequiredInt(parameters, "area");
        int worldLevel = GetRequiredInt(parameters, "worldLevel", "height");
        int current = GetLargestPlateauArea(grid, worldLevel);
        bool completed = current >= area;
        return new ObjectiveProgress(type, current, area, completed);
    }

    private static ObjectiveProgress EvaluateStayUnderWeight(
        string type,
        Dictionary<string, object> parameters,
        Grid grid)
    {
        int maxMass = GetRequiredInt(parameters, "maxMass", "maxWeight");
        int mass = GetTotalMass(grid);
        bool completed = mass <= maxMass;
        return new ObjectiveProgress(type, mass, maxMass, completed);
    }

    private static ObjectiveProgress EvaluateSurviveRotations(
        string type,
        Dictionary<string, object> parameters,
        int rotationsExecuted)
    {
        int target = GetRequiredInt(parameters, "rotations", "count", "k");
        bool completed = rotationsExecuted >= target;
        return new ObjectiveProgress(type, rotationsExecuted, target, completed);
    }

    private static string NormalizeType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[type.Length];
        int index = 0;
        foreach (char c in type)
        {
            if (c is '_' or '-' or ' ')
            {
                continue;
            }

            buffer[index] = char.ToUpperInvariant(c);
            index++;
        }

        return new string(buffer[..index]);
    }

    private static int GetRequiredInt(Dictionary<string, object> parameters, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (TryGetInt(parameters, key, out int value))
            {
                return value;
            }
        }

        throw new ArgumentException($"Missing required integer parameter(s): {string.Join(", ", keys)}.");
    }

    private static bool TryGetInt(Dictionary<string, object> parameters, string key, out int value)
    {
        if (!parameters.TryGetValue(key, out object? raw))
        {
            value = 0;
            return false;
        }

        value = raw switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
            JsonElement element when element.ValueKind == JsonValueKind.String => ParseStringInt(element.GetString(), key),
            _ => throw new ArgumentException($"Objective parameter '{key}' must be an integer.")
        };

        return true;
    }

    private static int ParseStringInt(string? text, string key)
    {
        if (int.TryParse(text, out int value))
        {
            return value;
        }

        throw new ArgumentException($"Objective parameter '{key}' must be an integer.");
    }

    private static int GetMaxWorldHeight(Grid grid)
    {
        int max = -1;
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Voxel voxel = grid.GetVoxel(new Int3(x, y, z));
                    if (!IsSolidForObjectives(voxel.Type))
                    {
                        continue;
                    }

                    if (y > max)
                    {
                        max = y;
                    }
                }
            }
        }

        return max;
    }

    private static int GetTotalMass(Grid grid)
    {
        int total = 0;
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Voxel voxel = grid.GetVoxel(new Int3(x, y, z));
                    if (voxel.Type != OccupancyType.Solid)
                    {
                        continue;
                    }

                    total += GetMaterialMass(voxel.MaterialId);
                }
            }
        }

        return total;
    }

    private static int GetMaterialMass(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
        {
            return 1;
        }

        string normalized = materialId.Trim().ToUpperInvariant();
        return normalized switch
        {
            "HEAVY" => 2,
            "STANDARD" => 1,
            "REINFORCED" => 1,
            _ => 1
        };
    }

    private static int GetLargestPlateauArea(Grid grid, int worldLevel)
    {
        int sizeX = grid.Size.X;
        int sizeZ = grid.Size.Z;
        bool[,] visited = new bool[sizeX, sizeZ];
        int largest = 0;

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                if (visited[x, z])
                {
                    continue;
                }

                Int3 pos = new(x, worldLevel, z);
                if (!grid.IsInBounds(pos))
                {
                    visited[x, z] = true;
                    continue;
                }

                Voxel voxel = grid.GetVoxel(pos);
                if (!IsSolidForObjectives(voxel.Type))
                {
                    visited[x, z] = true;
                    continue;
                }

                int area = FloodFillPlateau(grid, visited, worldLevel, x, z);
                if (area > largest)
                {
                    largest = area;
                }
            }
        }

        return largest;
    }

    private static int FloodFillPlateau(Grid grid, bool[,] visited, int worldLevel, int startX, int startZ)
    {
        Queue<Int3> queue = new();
        queue.Enqueue(new Int3(startX, worldLevel, startZ));
        visited[startX, startZ] = true;

        int area = 0;
        while (queue.Count > 0)
        {
            Int3 current = queue.Dequeue();
            area++;

            foreach (Int3 offset in PlateauOffsets)
            {
                Int3 next = current + offset;
                if (!grid.IsInBounds(next))
                {
                    continue;
                }

                if (next.Y != worldLevel)
                {
                    continue;
                }

                if (visited[next.X, next.Z])
                {
                    continue;
                }

                Voxel voxel = grid.GetVoxel(next);
                if (!IsSolidForObjectives(voxel.Type))
                {
                    visited[next.X, next.Z] = true;
                    continue;
                }

                visited[next.X, next.Z] = true;
                queue.Enqueue(next);
            }
        }

        return area;
    }

    private static bool IsSolidForObjectives(OccupancyType type) =>
        type is OccupancyType.Solid or OccupancyType.Wall or OccupancyType.Bedrock or OccupancyType.Ice
            or OccupancyType.Drain or OccupancyType.Porous;
}
