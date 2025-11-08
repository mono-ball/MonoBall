namespace PokeSharp.Core.Factories;

/// <summary>
///     Result of template validation before entity spawning.
///     Provides detailed error messages for debugging invalid templates.
/// </summary>
public sealed class TemplateValidationResult
{
    /// <summary>
    ///     Template ID that was validated.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    ///     Whether the template is valid and safe to spawn.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    ///     List of validation errors (empty if valid).
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    ///     List of validation warnings (non-fatal issues).
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    ///     Timestamp when validation was performed.
    /// </summary>
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Create a successful validation result.
    /// </summary>
    public static TemplateValidationResult Success(string templateId)
    {
        return new TemplateValidationResult { TemplateId = templateId, IsValid = true };
    }

    /// <summary>
    ///     Create a failed validation result with errors.
    /// </summary>
    public static TemplateValidationResult Failure(string templateId, params string[] errors)
    {
        return new TemplateValidationResult
        {
            TemplateId = templateId,
            IsValid = false,
            Errors = errors.ToList(),
        };
    }

    /// <summary>
    ///     Create a validation result with warnings (still valid).
    /// </summary>
    public static TemplateValidationResult WithWarnings(string templateId, params string[] warnings)
    {
        return new TemplateValidationResult
        {
            TemplateId = templateId,
            IsValid = true,
            Warnings = warnings.ToList(),
        };
    }

    public override string ToString()
    {
        if (IsValid && Warnings.Count == 0)
            return $"Template '{TemplateId}' is valid";

        if (IsValid && Warnings.Count > 0)
            return $"Template '{TemplateId}' is valid with {Warnings.Count} warning(s)";

        return $"Template '{TemplateId}' validation failed: {string.Join(", ", Errors)}";
    }
}
