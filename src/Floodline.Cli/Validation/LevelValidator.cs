using System.Text.Json;
using System.Globalization;
using Floodline.Core;
using Floodline.Core.Levels;
using Json.Pointer;
using Json.Schema;

namespace Floodline.Cli.Validation;

public static class LevelValidator
{
    private const string DefaultSchemaVersion = "0.2.2";
    private const int RepoSearchDepth = 12;
    private static readonly HashSet<string> AllowedGravityDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "DOWN",
        "NORTH",
        "SOUTH",
        "EAST",
        "WEST"
    };
    private static readonly HashSet<string> AllowedBagMaterials = new(StringComparer.OrdinalIgnoreCase)
    {
        "STANDARD",
        "HEAVY",
        "REINFORCED"
    };

    public static LevelValidationResult ValidateFile(string levelPath)
    {
        if (string.IsNullOrWhiteSpace(levelPath))
        {
            return new LevelValidationResult(
                [new LevelValidationError("<unknown>", "#", "io.level_path_missing", "Level path is missing.")]);
        }

        if (!File.Exists(levelPath))
        {
            return new LevelValidationResult(
                [new LevelValidationError(levelPath, "#", "io.level_not_found", "Level file not found.")]);
        }

        string json = File.ReadAllText(levelPath);
        return ValidateJson(levelPath, json);
    }

    public static LevelValidationResult ValidateJson(string levelPath, string json)
    {
        List<LevelValidationError> errors = [];
        if (!TryParseJson(levelPath, json, out JsonDocument? document, out LevelValidationError? parseError))
        {
            errors.Add(parseError!);
            return new LevelValidationResult(errors);
        }

        JsonDocument parsed = document!;
        using (parsed)
        {
            string schemaVersion = TryGetSchemaVersion(parsed.RootElement) ?? DefaultSchemaVersion;
            if (!TryFindRepoRoot(levelPath, out string? repoRoot))
            {
                errors.Add(new LevelValidationError(levelPath, "#", "schema.repo_root_not_found", "Could not locate repo root (Floodline.sln) to resolve schemas."));
                return new LevelValidationResult(errors);
            }

            string schemaPath = Path.Combine(repoRoot!, "schemas", $"level.schema.v{schemaVersion}.json");
            if (!File.Exists(schemaPath))
            {
                string pointer = "#/meta/schemaVersion";
                errors.Add(new LevelValidationError(levelPath, pointer, "schema.file_not_found", $"Schema file not found: {schemaPath}"));
                return new LevelValidationResult(errors);
            }

            string schemaJson = File.ReadAllText(schemaPath);
            BuildOptions buildOptions = new()
            {
                SchemaRegistry = new SchemaRegistry()
            };
            JsonSchema schema = JsonSchema.FromText(schemaJson, buildOptions);
            EvaluationOptions options = new()
            {
                OutputFormat = OutputFormat.List
            };

            EvaluationResults results = schema.Evaluate(parsed.RootElement, options);
            CollectSchemaErrors(results, levelPath, errors);

            if (errors.Count == 0)
            {
                ValidateSemantics(levelPath, json, errors);
            }
        }

        return new LevelValidationResult(errors);
    }

    private static bool TryParseJson(string levelPath, string json, out JsonDocument? document, out LevelValidationError? error)
    {
        try
        {
            document = JsonDocument.Parse(json);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            document = null;
            error = new LevelValidationError(levelPath, "#", "json.parse", ex.Message);
            return false;
        }
    }

    private static string? TryGetSchemaVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty("meta", out JsonElement meta) || meta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!meta.TryGetProperty("schemaVersion", out JsonElement version))
        {
            return null;
        }

        return version.ValueKind == JsonValueKind.String ? version.GetString() : null;
    }

    private static bool TryFindRepoRoot(string levelPath, out string? repoRoot)
    {
        DirectoryInfo? dir = new FileInfo(levelPath).Directory;
        for (int i = 0; i < RepoSearchDepth && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Floodline.sln")))
            {
                repoRoot = dir.FullName;
                return true;
            }

            dir = dir.Parent;
        }

        repoRoot = null;
        return false;
    }

    private static void CollectSchemaErrors(EvaluationResults results, string levelPath, List<LevelValidationError> errors)
    {
        if (results.IsValid)
        {
            return;
        }

        if (results.Errors is not null)
        {
            foreach (KeyValuePair<string, string> error in results.Errors)
            {
                string pointer = NormalizePointer(results.InstanceLocation);
                string ruleId = $"schema.{error.Key}";
                errors.Add(new LevelValidationError(levelPath, pointer, ruleId, error.Value));
            }
        }

        if (results.Details is not null)
        {
            foreach (EvaluationResults detail in results.Details)
            {
                CollectSchemaErrors(detail, levelPath, errors);
            }
        }
    }

    private static string NormalizePointer(JsonPointer? pointer)
    {
        if (pointer is null)
        {
            return "#";
        }

        string? text = pointer.ToString();
        return string.IsNullOrWhiteSpace(text) ? "#" : text;
    }

    private static void ValidateSemantics(string levelPath, string json, List<LevelValidationError> errors)
    {
        Level level;
        try
        {
            level = LevelLoader.Load(json);
        }
        catch (Exception ex)
        {
            errors.Add(new LevelValidationError(levelPath, "#", "semantic.level_loader", ex.Message));
            return;
        }

        ValidateInitialVoxels(level, levelPath, errors);
        ValidateBag(level, levelPath, errors);
        ValidateRotation(level, levelPath, errors);
        ValidateObjectives(level, levelPath, errors);
        ValidateHazards(level, levelPath, errors);
    }

    private static void ValidateInitialVoxels(Level level, string levelPath, List<LevelValidationError> errors)
    {
        Int3 bounds = level.Bounds;
        HashSet<Int3> seen = [];

        for (int i = 0; i < level.InitialVoxels.Count; i++)
        {
            VoxelData voxel = level.InitialVoxels[i];
            Int3 pos = voxel.Pos;
            string pointer = $"#/initialVoxels/{i}/pos";

            if (pos.X >= bounds.X || pos.Y >= bounds.Y || pos.Z >= bounds.Z)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    pointer,
                    "semantic.initial_voxel.out_of_bounds",
                    $"Voxel position {pos} is outside bounds {bounds}."));
            }

            if (!seen.Add(pos))
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    pointer,
                    "semantic.initial_voxel.duplicate",
                    $"Duplicate voxel position {pos} detected."));
            }
        }
    }

    private static void ValidateBag(Level level, string levelPath, List<LevelValidationError> errors)
    {
        string typeRaw = level.Bag.Type ?? string.Empty;
        if (string.IsNullOrWhiteSpace(typeRaw))
        {
            errors.Add(new LevelValidationError(levelPath, "#/bag/type", "semantic.bag.type_missing", "Bag type is missing."));
            return;
        }

        string normalized = NormalizeBagType(typeRaw);
        bool isFixed = normalized is "FIXED_SEQUENCE" or "FIXED";
        bool isWeighted = normalized is "WEIGHTED";

        if (!isFixed && !isWeighted)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                "#/bag/type",
                "semantic.bag.type_invalid",
                $"Bag type '{typeRaw}' is not supported. Use FIXED_SEQUENCE or WEIGHTED."));
            return;
        }

        if (isFixed)
        {
            if (level.Bag.Sequence is null || level.Bag.Sequence.Length == 0)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    "#/bag/sequence",
                    "semantic.bag.sequence_missing",
                    "Fixed-sequence bag requires a non-empty sequence."));
            }
            else
            {
                ValidateBagSequence(levelPath, errors, level.Bag.Sequence);
            }
        }

        if (isWeighted)
        {
            if (level.Bag.Weights is null || level.Bag.Weights.Count == 0)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    "#/bag/weights",
                    "semantic.bag.weights_missing",
                    "Weighted bag requires at least one weight entry."));
            }
            else
            {
                ValidateBagWeights(levelPath, errors, level.Bag.Weights);
            }
        }
    }

    private static string NormalizeBagType(string typeRaw) =>
        typeRaw.Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .ToUpperInvariant();

    private static void ValidateBagSequence(string levelPath, List<LevelValidationError> errors, string[] sequence)
    {
        for (int i = 0; i < sequence.Length; i++)
        {
            string token = sequence[i];
            if (!TryValidateBagToken(levelPath, errors, $"#/bag/sequence/{i}", token))
            {
                continue;
            }
        }
    }

    private static void ValidateBagWeights(
        string levelPath,
        List<LevelValidationError> errors,
        Dictionary<string, int> weights)
    {
        bool hasPositive = false;

        foreach (KeyValuePair<string, int> entry in weights)
        {
            string key = entry.Key;
            string pointer = $"#/bag/weights/{EscapePointerToken(key)}";

            if (!TryValidateBagToken(levelPath, errors, pointer, key))
            {
                continue;
            }

            int weight = entry.Value;
            if (weight < 0)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    pointer,
                    "semantic.bag.weight_negative",
                    $"Weight for '{key}' must be >= 0."));
            }

            if (weight > 0)
            {
                hasPositive = true;
            }
        }

        if (!hasPositive)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                "#/bag/weights",
                "semantic.bag.weights_empty",
                "Weighted bag must include at least one positive weight."));
        }
    }

    private static bool TryValidateBagToken(
        string levelPath,
        List<LevelValidationError> errors,
        string pointer,
        string token)
    {
        if (!TryParseBagToken(token, out string pieceId, out string? material))
        {
            errors.Add(new LevelValidationError(
                levelPath,
                pointer,
                "semantic.bag.token_invalid",
                $"Bag entry '{token}' is not a valid piece token."));
            return false;
        }

        if (!IsValidPieceId(pieceId))
        {
            errors.Add(new LevelValidationError(
                levelPath,
                pointer,
                "semantic.bag.token_invalid",
                $"Piece id '{pieceId}' is not recognized."));
            return false;
        }

        if (material is not null && !IsValidMaterialId(material))
        {
            errors.Add(new LevelValidationError(
                levelPath,
                pointer,
                "semantic.bag.material_invalid",
                $"Material id '{material}' is not supported."));
            return false;
        }

        return true;
    }

    private static bool TryParseBagToken(string token, out string pieceId, out string? material)
    {
        pieceId = string.Empty;
        material = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string trimmed = token.Trim();
        int separatorIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            pieceId = trimmed;
            return true;
        }

        if (separatorIndex == 0 || separatorIndex == trimmed.Length - 1)
        {
            return false;
        }

        if (trimmed.IndexOf(':', separatorIndex + 1) >= 0)
        {
            return false;
        }

        pieceId = trimmed[..separatorIndex];
        material = trimmed[(separatorIndex + 1)..];
        return true;
    }

    private static bool IsValidPieceId(string value) =>
        Enum.TryParse<PieceId>(value.Trim(), ignoreCase: true, out PieceId _);

    private static bool IsValidMaterialId(string value) =>
        AllowedBagMaterials.Contains(value.Trim());

    private static void ValidateRotation(Level level, string levelPath, List<LevelValidationError> errors)
    {
        RotationConfig rotation = level.Rotation;

        if (rotation.MaxRotations is < 0)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                "#/rotation/maxRotations",
                "semantic.rotation.max_rotations_negative",
                "maxRotations must be >= 0."));
        }

        if (rotation.TiltBudget is < 0)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                "#/rotation/tiltBudget",
                "semantic.rotation.tilt_budget_negative",
                "tiltBudget must be >= 0."));
        }

        if (rotation.CooldownTicks is < 0)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                "#/rotation/cooldownTicks",
                "semantic.rotation.cooldown_negative",
                "cooldownTicks must be >= 0."));
        }

        if (rotation.AllowedDirections is null)
        {
            return;
        }

        if (rotation.AllowedDirections.Length == 0)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                "#/rotation/allowedDirections",
                "semantic.rotation.allowed_directions_empty",
                "allowedDirections must include at least one direction."));
            return;
        }

        for (int i = 0; i < rotation.AllowedDirections.Length; i++)
        {
            string? direction = rotation.AllowedDirections[i];
            if (string.IsNullOrWhiteSpace(direction))
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/rotation/allowedDirections/{i}",
                    "semantic.rotation.allowed_direction_missing",
                    "allowedDirections entry must be a non-empty string."));
                continue;
            }

            string normalized = direction.Trim().ToUpperInvariant();
            if (normalized == "UP")
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/rotation/allowedDirections/{i}",
                    "semantic.rotation.allowed_direction_up",
                    "UP is not allowed as a gameplay gravity direction."));
                continue;
            }

            if (!AllowedGravityDirections.Contains(normalized))
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/rotation/allowedDirections/{i}",
                    "semantic.rotation.allowed_direction_invalid",
                    $"Gravity direction '{direction}' is not valid."));
            }
        }
    }

    private static void ValidateObjectives(Level level, string levelPath, List<LevelValidationError> errors)
    {
        for (int i = 0; i < level.Objectives.Count; i++)
        {
            ObjectiveConfig objective = level.Objectives[i];
            string typeRaw = objective.Type ?? string.Empty;
            string pointer = $"#/objectives/{i}/type";

            if (string.IsNullOrWhiteSpace(typeRaw))
            {
                errors.Add(new LevelValidationError(levelPath, pointer, "semantic.objective.type_missing", "Objective type is missing."));
                continue;
            }

            if (objective.Params is null)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/objectives/{i}/params",
                    "semantic.objective.params_missing",
                    "Objective params are missing."));
                continue;
            }

            Dictionary<string, object> parameters = objective.Params;
            string normalized = NormalizeType(typeRaw);
            switch (normalized)
            {
                case "DRAINWATER":
                    RequireInt(levelPath, errors, $"#/objectives/{i}/params", parameters, ["targetUnits", "units", "target"]);
                    RejectUnknownParams(levelPath, errors, $"#/objectives/{i}/params", parameters, ["targetUnits", "units", "target"], "semantic.objective.param_unknown", "objective DRAIN_WATER");
                    break;
                case "REACHHEIGHT":
                    RequireInt(levelPath, errors, $"#/objectives/{i}/params", parameters, ["height", "worldHeight"]);
                    RejectUnknownParams(levelPath, errors, $"#/objectives/{i}/params", parameters, ["height", "worldHeight"], "semantic.objective.param_unknown", "objective REACH_HEIGHT");
                    break;
                case "BUILDPLATEAU":
                    RequireInt(levelPath, errors, $"#/objectives/{i}/params", parameters, ["area"]);
                    RequireInt(levelPath, errors, $"#/objectives/{i}/params", parameters, ["worldLevel", "height"]);
                    RejectUnknownParams(levelPath, errors, $"#/objectives/{i}/params", parameters, ["area", "worldLevel", "height"], "semantic.objective.param_unknown", "objective BUILD_PLATEAU");
                    break;
                case "STAYUNDERWEIGHT":
                    RequireInt(levelPath, errors, $"#/objectives/{i}/params", parameters, ["maxMass", "maxWeight"]);
                    RejectUnknownParams(levelPath, errors, $"#/objectives/{i}/params", parameters, ["maxMass", "maxWeight"], "semantic.objective.param_unknown", "objective STAY_UNDER_WEIGHT");
                    break;
                case "SURVIVEROTATIONS":
                    RequireInt(levelPath, errors, $"#/objectives/{i}/params", parameters, ["rotations", "count", "k"]);
                    RejectUnknownParams(levelPath, errors, $"#/objectives/{i}/params", parameters, ["rotations", "count", "k"], "semantic.objective.param_unknown", "objective SURVIVE_ROTATIONS");
                    break;
                default:
                    errors.Add(new LevelValidationError(
                        levelPath,
                        pointer,
                        "semantic.objective.type_invalid",
                        $"Objective type '{typeRaw}' is not recognized for MVP."));
                    break;
            }
        }
    }

    private static void ValidateHazards(Level level, string levelPath, List<LevelValidationError> errors)
    {
        for (int i = 0; i < level.Hazards.Count; i++)
        {
            HazardConfig hazard = level.Hazards[i];
            string typeRaw = hazard.Type ?? string.Empty;
            string pointer = $"#/hazards/{i}/type";

            if (string.IsNullOrWhiteSpace(typeRaw))
            {
                errors.Add(new LevelValidationError(levelPath, pointer, "semantic.hazard.type_missing", "Hazard type is missing."));
                continue;
            }

            if (hazard.Params is null)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/hazards/{i}/params",
                    "semantic.hazard.params_missing",
                    "Hazard params are missing."));
                continue;
            }

            Dictionary<string, object> parameters = hazard.Params;
            string normalized = NormalizeType(typeRaw);
            switch (normalized)
            {
                case "WINDGUST":
                    ValidateWindGust(levelPath, errors, i, parameters);
                    break;
                default:
                    errors.Add(new LevelValidationError(
                        levelPath,
                        pointer,
                        "semantic.hazard.type_invalid",
                        $"Hazard type '{typeRaw}' is not recognized for MVP."));
                    break;
            }
        }
    }

    private static void ValidateWindGust(string levelPath, List<LevelValidationError> errors, int hazardIndex, Dictionary<string, object> parameters)
    {
        int intervalTicks = RequireInt(levelPath, errors, $"#/hazards/{hazardIndex}/params", parameters, ["intervalTicks"]);
        int pushStrength = RequireInt(levelPath, errors, $"#/hazards/{hazardIndex}/params", parameters, ["pushStrength"]);

        if (intervalTicks < 1)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                $"#/hazards/{hazardIndex}/params/intervalTicks",
                "semantic.wind.interval_ticks_invalid",
                "intervalTicks must be >= 1."));
        }

        if (pushStrength < 1)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                $"#/hazards/{hazardIndex}/params/pushStrength",
                "semantic.wind.push_strength_invalid",
                "pushStrength must be >= 1."));
        }

        string? directionMode = RequireString(levelPath, errors, $"#/hazards/{hazardIndex}/params/directionMode", parameters, "directionMode");
        if (string.IsNullOrWhiteSpace(directionMode))
        {
            return;
        }

        string modeNormalized = directionMode.Trim().ToUpperInvariant();
        if (modeNormalized is not ("ALTERNATE_EW" or "FIXED" or "RANDOM_SEEDED"))
        {
            errors.Add(new LevelValidationError(
                levelPath,
                $"#/hazards/{hazardIndex}/params/directionMode",
                "semantic.wind.direction_mode_invalid",
                $"directionMode '{directionMode}' is invalid. Use ALTERNATE_EW, FIXED, or RANDOM_SEEDED."));
            return;
        }

        if (TryGetInt(parameters, "firstGustOffsetTicks", out int offset))
        {
            if (offset < 0)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/hazards/{hazardIndex}/params/firstGustOffsetTicks",
                    "semantic.wind.first_offset_negative",
                    "firstGustOffsetTicks must be >= 0."));
            }
        }

        bool hasFixedDirection = TryGetString(parameters, "fixedDirection", out string? fixedDir) && !string.IsNullOrWhiteSpace(fixedDir);
        if (modeNormalized == "FIXED")
        {
            if (!hasFixedDirection)
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/hazards/{hazardIndex}/params/fixedDirection",
                    "semantic.wind.fixed_direction_missing",
                    "fixedDirection is required when directionMode is FIXED."));
                return;
            }

            string dirNorm = fixedDir!.Trim().ToUpperInvariant();
            if (dirNorm is not ("EAST" or "WEST" or "NORTH" or "SOUTH"))
            {
                errors.Add(new LevelValidationError(
                    levelPath,
                    $"#/hazards/{hazardIndex}/params/fixedDirection",
                    "semantic.wind.fixed_direction_invalid",
                $"fixedDirection '{fixedDir}' is invalid. Use EAST/WEST/NORTH/SOUTH."));
            }
        }
        else if (hasFixedDirection)
        {
            errors.Add(new LevelValidationError(
                levelPath,
                $"#/hazards/{hazardIndex}/params/fixedDirection",
                "semantic.wind.fixed_direction_unexpected",
                "fixedDirection must not be provided unless directionMode is FIXED."));
        }

        RejectUnknownParams(
            levelPath,
            errors,
            $"#/hazards/{hazardIndex}/params",
            parameters,
            ["intervalTicks", "pushStrength", "directionMode", "firstGustOffsetTicks", "fixedDirection"],
            "semantic.hazard.param_unknown",
            "hazard WIND_GUST");
    }

    private static int RequireInt(
        string levelPath,
        List<LevelValidationError> errors,
        string paramsPointer,
        Dictionary<string, object> parameters,
        IReadOnlyList<string> keys)
    {
        foreach (string key in keys)
        {
            if (!parameters.TryGetValue(key, out object? raw) || raw is null)
            {
                continue;
            }

            if (TryConvertInt(raw, out int value))
            {
                return value;
            }

            errors.Add(new LevelValidationError(
                levelPath,
                $"{paramsPointer}/{key}",
                "semantic.params.int_invalid",
                $"Parameter '{key}' must be an integer."));
            return 0;
        }

        errors.Add(new LevelValidationError(
            levelPath,
            paramsPointer,
            "semantic.params.int_missing",
            $"Missing required integer param (expected one of: {string.Join(", ", keys)})."));
        return 0;
    }

    private static bool TryGetInt(Dictionary<string, object> parameters, string key, out int value)
    {
        if (!parameters.TryGetValue(key, out object? raw) || raw is null)
        {
            value = 0;
            return false;
        }

        return TryConvertInt(raw, out value);
    }

    private static bool TryConvertInt(object raw, out int value)
    {
        try
        {
            value = raw switch
            {
                int i => i,
                long l => checked((int)l),
                JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
                JsonElement element when element.ValueKind == JsonValueKind.String => int.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
                string s => int.Parse(s, CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"Unsupported value type '{raw.GetType()}'")
            };

            return true;
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static string? RequireString(
        string levelPath,
        List<LevelValidationError> errors,
        string pointer,
        Dictionary<string, object> parameters,
        string key)
    {
        if (!TryGetString(parameters, key, out string? value))
        {
            errors.Add(new LevelValidationError(levelPath, pointer, "semantic.params.string_missing", $"Missing required string param '{key}'."));
            return null;
        }

        return value;
    }

    private static bool TryGetString(Dictionary<string, object> parameters, string key, out string? value)
    {
        if (!parameters.TryGetValue(key, out object? raw) || raw is null)
        {
            value = null;
            return false;
        }

        value = raw switch
        {
            string s => s,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => null
        };

        return value is not null;
    }

    private static void RejectUnknownParams(
        string levelPath,
        List<LevelValidationError> errors,
        string paramsPointer,
        Dictionary<string, object> parameters,
        IReadOnlyList<string> allowedKeys,
        string ruleId,
        string context)
    {
        HashSet<string> allowed = new(allowedKeys, StringComparer.OrdinalIgnoreCase);

        foreach (string key in parameters.Keys)
        {
            if (allowed.Contains(key))
            {
                continue;
            }

            errors.Add(new LevelValidationError(
                levelPath,
                $"{paramsPointer}/{EscapePointerToken(key)}",
                ruleId,
                $"Parameter '{key}' is not supported for {context}."));
        }
    }

    private static string EscapePointerToken(string token) =>
        token.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string NormalizeType(string typeRaw)
    {
        if (string.IsNullOrWhiteSpace(typeRaw))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[typeRaw.Length];
        int index = 0;
        foreach (char c in typeRaw)
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
}
