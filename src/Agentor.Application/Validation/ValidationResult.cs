namespace Agentor.Application.Validation;

public sealed record ValidationResult
{
    private ValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public bool IsValid { get; }

    public IReadOnlyList<string> Errors { get; }

    public static ValidationResult Ok() => new(true, []);

    public static ValidationResult Fail(IReadOnlyList<string> errors) => new(false, errors);
}
