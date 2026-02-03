namespace Floodline.Cli.Validation;

public sealed record LevelValidationResult(IReadOnlyList<LevelValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
