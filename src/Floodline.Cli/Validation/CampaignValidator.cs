using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Json.Pointer;
using Json.Schema;

namespace Floodline.Cli.Validation;

public static class CampaignValidator
{
    private const string DefaultSchemaVersion = "0.2.1";
    private const string DefaultCampaignFileName = "campaign.v0.2.0.json";
    private const int RepoSearchDepth = 12;

    public static LevelValidationResult ValidateFile(string? campaignPath)
    {
        if (!TryResolveCampaignPath(campaignPath, out string? resolvedPath, out LevelValidationError? pathError))
        {
            return new LevelValidationResult([pathError!]);
        }

        if (!File.Exists(resolvedPath))
        {
            return new LevelValidationResult(
                [new LevelValidationError(resolvedPath!, "#", "io.campaign_not_found", "Campaign file not found.")]);
        }

        string json = File.ReadAllText(resolvedPath!);
        return ValidateJson(resolvedPath!, json);
    }

    private static bool TryResolveCampaignPath(string? campaignPath, out string? resolvedPath, out LevelValidationError? error)
    {
        if (!TryFindRepoRoot(campaignPath, out string? repoRoot))
        {
            resolvedPath = null;
            string path = string.IsNullOrWhiteSpace(campaignPath) ? "<unknown>" : campaignPath;
            error = new LevelValidationError(path, "#", "schema.repo_root_not_found", "Could not locate repo root (Floodline.sln) to resolve campaign path.");
            return false;
        }

        string pathValue = campaignPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            resolvedPath = Path.Combine(repoRoot!, "levels", DefaultCampaignFileName);
            error = null;
            return true;
        }

        resolvedPath = Path.IsPathRooted(pathValue)
            ? pathValue
            : Path.Combine(repoRoot!, pathValue);
        error = null;
        return true;
    }

    public static LevelValidationResult ValidateJson(string campaignPath, string json)
    {
        List<LevelValidationError> errors = [];
        if (!TryParseJson(campaignPath, json, out JsonDocument? document, out LevelValidationError? parseError))
        {
            errors.Add(parseError!);
            return new LevelValidationResult(errors);
        }

        JsonDocument parsed = document!;
        using (parsed)
        {
            if (!TryFindRepoRoot(campaignPath, out string? repoRoot))
            {
                errors.Add(new LevelValidationError(campaignPath, "#", "schema.repo_root_not_found", "Could not locate repo root (Floodline.sln) to resolve schemas."));
                return new LevelValidationResult(errors);
            }

            string schemaVersion = TryGetSchemaVersion(parsed.RootElement) ?? DefaultSchemaVersion;
            string schemaPath = Path.Combine(repoRoot!, "schemas", $"campaign.schema.v{schemaVersion}.json");
            if (!File.Exists(schemaPath))
            {
                string pointer = "#/meta/schemaVersion";
                errors.Add(new LevelValidationError(campaignPath, pointer, "schema.file_not_found", $"Schema file not found: {schemaPath}"));
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
            CollectSchemaErrors(results, campaignPath, errors);

            if (errors.Count == 0)
            {
                ValidateLevelReferences(parsed.RootElement, campaignPath, repoRoot!, errors);
            }
        }

        return new LevelValidationResult(errors);
    }

    private static bool TryParseJson(string campaignPath, string json, out JsonDocument? document, out LevelValidationError? error)
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
            error = new LevelValidationError(campaignPath, "#", "json.parse", ex.Message);
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

    private static bool TryFindRepoRoot(string? pathHint, out string? repoRoot)
    {
        DirectoryInfo? dir = null;
        if (!string.IsNullOrWhiteSpace(pathHint))
        {
            dir = new FileInfo(pathHint).Directory;
        }

        dir ??= new DirectoryInfo(AppContext.BaseDirectory);

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

    private static void CollectSchemaErrors(EvaluationResults results, string campaignPath, List<LevelValidationError> errors)
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
                errors.Add(new LevelValidationError(campaignPath, pointer, ruleId, error.Value));
            }
        }

        if (results.Details is not null)
        {
            foreach (EvaluationResults detail in results.Details)
            {
                CollectSchemaErrors(detail, campaignPath, errors);
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

    private static void ValidateLevelReferences(
        JsonElement root,
        string campaignPath,
        string repoRoot,
        List<LevelValidationError> errors)
    {
        if (!root.TryGetProperty("levels", out JsonElement levels) || levels.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int index = 0;
        foreach (JsonElement levelRef in levels.EnumerateArray())
        {
            string pointerBase = $"#/levels/{index}";
            if (levelRef.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            if (!levelRef.TryGetProperty("path", out JsonElement pathElement) || pathElement.ValueKind != JsonValueKind.String)
            {
                index++;
                continue;
            }

            string pathValue = pathElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                errors.Add(new LevelValidationError(
                    campaignPath,
                    $"{pointerBase}/path",
                    "campaign.level_path_missing",
                    "Level path is missing."));
                index++;
                continue;
            }

            string resolvedLevelPath = Path.IsPathRooted(pathValue)
                ? pathValue
                : Path.Combine(repoRoot, pathValue);

            if (!File.Exists(resolvedLevelPath))
            {
                errors.Add(new LevelValidationError(
                    campaignPath,
                    $"{pointerBase}/path",
                    "campaign.level_not_found",
                    $"Level file not found: {resolvedLevelPath}"));
                index++;
                continue;
            }

            LevelValidationResult levelResult = LevelValidator.ValidateFile(resolvedLevelPath);
            if (!levelResult.IsValid)
            {
                errors.AddRange(levelResult.Errors);
            }

            index++;
        }
    }
}
