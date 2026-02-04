using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Floodline.Core.Levels;

/// <summary>
/// Loads and validates level data from JSON.
/// </summary>
public static class LevelLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Loads a level from a JSON string.
    /// </summary>
    /// <param name="json">The JSON content.</param>
    /// <returns>A validated Level object.</returns>
    /// <exception cref="ArgumentException">Thrown if JSON is invalid or violates rules.</exception>
    public static Level Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Level JSON cannot be empty.");
        }

        try
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                ValidateNoFloats(doc.RootElement, "");
            }

            Level level = JsonSerializer.Deserialize<Level>(json, Options) ?? throw new ArgumentException("Failed to deserialize level.");
            level = NormalizeLevel(level);

            ValidateLevel(level);

            return level;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON format: {ex.Message}", ex);
        }
    }

    private static Level NormalizeLevel(Level level)
    {
        AbilitiesConfig abilities = level.Abilities ?? new AbilitiesConfig();
        ConstraintsConfig constraints = level.Constraints ?? new ConstraintsConfig();

        List<HazardConfig> hazards = level.Hazards ?? [];
        for (int i = 0; i < hazards.Count; i++)
        {
            HazardConfig hazard = hazards[i];
            if (hazard.Params is null)
            {
                hazards[i] = hazard with { Params = [] };
            }
        }

        return level with
        {
            Abilities = abilities,
            Constraints = constraints,
            Hazards = hazards
        };
    }

    private static void ValidateNoFloats(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    ValidateNoFloats(property.Value, string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}");
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ValidateNoFloats(item, $"{path}[{index}]");
                    index++;
                }
                break;
            case JsonValueKind.Number:
                // System.Text.Json doesn't directly tell us if it's a float or int without trying to parse.
                // If it contains a decimal point or scientific notation, it's a float in our book for durations/ticks.
                // However, the rules say "reject floats" for durations. 
                // A safer check is to see if it can be represented as a long without loss.
                string raw = element.GetRawText();
                if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E'))
                {
                    // Only reject if it's in a sensitive area? 
                    // Actually, the requirement says "Authoring rule: any time/duration stored as integer ticks; reject floats".
                    // To be safe and strict, we reject any float in the level JSON for MVP.
                    throw new ArgumentException($"Floating point number found at '{path}': {raw}. Only integers are allowed.");
                }
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                break;
        }
    }

    private static void ValidateLevel(Level level)
    {
        if (level.Meta == null)
        {
            throw new ArgumentException("Level metadata is missing.");
        }

        if (string.IsNullOrWhiteSpace(level.Meta.Id))
        {
            throw new ArgumentException("Level ID is missing.");
        }

        if (string.IsNullOrWhiteSpace(level.Meta.SchemaVersion))
        {
            throw new ArgumentException("Level schemaVersion is missing.");
        }

        if (level.Abilities is null)
        {
            throw new ArgumentException("Level abilities configuration is missing.");
        }

        if (level.Abilities.StabilizeCharges < 0)
        {
            throw new ArgumentException("stabilizeCharges must be >= 0.");
        }

        if (level.Constraints is null)
        {
            throw new ArgumentException("Level constraints configuration is missing.");
        }

        if (level.Constraints.MaxWorldHeight is < 0)
        {
            throw new ArgumentException("maxWorldHeight must be >= 0.");
        }

        if (level.Constraints.MaxMass is < 0)
        {
            throw new ArgumentException("maxMass must be >= 0.");
        }

        if (level.Constraints.WaterForbiddenWorldHeightMin is < 0)
        {
            throw new ArgumentException("waterForbiddenWorldHeightMin must be >= 0.");
        }

        if (level.Bounds.X <= 0 || level.Bounds.Y <= 0 || level.Bounds.Z <= 0)
        {
            throw new ArgumentException("Level bounds must be positive.");
        }

        if (level.InitialVoxels == null)
        {
            throw new ArgumentException("InitialVoxels list is missing.");
        }

        if (level.Objectives == null)
        {
            throw new ArgumentException("Objectives list is missing.");
        }

        if (level.Rotation == null)
        {
            throw new ArgumentException("Rotation configuration is missing.");
        }

        ValidateObjectives(level);

        if (level.Bag == null)
        {
            throw new ArgumentException("Bag configuration is missing.");
        }

        if (string.IsNullOrWhiteSpace(level.Bag.Type))
        {
            throw new ArgumentException("Bag type is missing.");
        }

        if (level.Hazards == null)
        {
            throw new ArgumentException("Hazards list is missing.");
        }

        foreach (HazardConfig hazard in level.Hazards)
        {
            if (string.IsNullOrWhiteSpace(hazard.Type))
            {
                throw new ArgumentException("Hazard type is missing.");
            }
        }
    }

    private static void ValidateObjectives(Level level)
    {
        if (level.Objectives == null)
        {
            throw new ArgumentException("Objectives list is missing.");
        }

        foreach (ObjectiveConfig objective in level.Objectives)
        {
            if (string.IsNullOrWhiteSpace(objective.Type))
            {
                throw new ArgumentException("Objective type is missing.");
            }
        }
    }
}
