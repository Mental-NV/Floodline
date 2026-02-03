using System.Text.Json;
using Floodline.Core;
using Floodline.Core.Levels;
using Json.Pointer;
using Json.Schema;

namespace Floodline.Cli.Validation;

public static class LevelValidator
{
    private const string DefaultSchemaVersion = "0.2.0";
    private const int RepoSearchDepth = 12;

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
        }
    }

    private static string NormalizeBagType(string typeRaw) =>
        typeRaw.Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .ToUpperInvariant();
}
