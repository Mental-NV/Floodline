namespace Floodline.Cli.Validation;

public sealed record LevelValidationError(
    string FilePath,
    string JsonPointer,
    string RuleId,
    string Message);
