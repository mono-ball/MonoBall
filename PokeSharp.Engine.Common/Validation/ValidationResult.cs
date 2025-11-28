namespace PokeSharp.Engine.Common.Validation;

/// <summary>
///     Generic validation result for any validation operation.
///     Provides structured error and warning tracking with optional location information.
/// </summary>
/// <remarks>
///     <para>
///         This shared validation infrastructure is used across both Engine and Game layers
///         to maintain consistency in validation patterns and error reporting.
///     </para>
///     <example>
///         <code>
/// var result = new ValidationResult();
/// result.AddError("Invalid configuration", "config.json:15");
/// result.AddWarning("Deprecated property used");
///
/// if (!result.IsValid)
/// {
///     Console.WriteLine(result.GetErrorMessage());
/// }
/// </code>
///     </example>
/// </remarks>
public class ValidationResult
{
    /// <summary>
    ///     Whether the validation passed (no errors).
    ///     Warnings do not affect validity.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    ///     List of validation errors (must be fixed).
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    ///     List of validation warnings (should be reviewed).
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    ///     Optional context information about what was validated.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    ///     Adds an error to the validation result.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="location">Optional location where the error occurred</param>
    public void AddError(string message, string? location = null)
    {
        Errors.Add(new ValidationError(message, location));
    }

    /// <summary>
    ///     Adds a warning to the validation result.
    /// </summary>
    /// <param name="message">Warning message</param>
    /// <param name="location">Optional location where the warning occurred</param>
    public void AddWarning(string message, string? location = null)
    {
        Warnings.Add(new ValidationWarning(message, location));
    }

    /// <summary>
    ///     Gets a formatted error message for all errors.
    /// </summary>
    /// <returns>Formatted error message, or empty string if valid</returns>
    public string GetErrorMessage()
    {
        if (IsValid)
        {
            return string.Empty;
        }

        var lines = new List<string>();

        if (!string.IsNullOrEmpty(Context))
        {
            lines.Add($"Validation failed for {Context}:");
        }
        else
        {
            lines.Add("Validation failed:");
        }

        foreach (ValidationError error in Errors)
        {
            string location = error.Location != null ? $" (at {error.Location})" : "";
            lines.Add($"  - {error.Message}{location}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    ///     Gets a formatted warning message for all warnings.
    /// </summary>
    /// <returns>Formatted warning message, or empty string if no warnings</returns>
    public string GetWarningMessage()
    {
        if (Warnings.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();

        if (!string.IsNullOrEmpty(Context))
        {
            lines.Add($"Validation warnings for {Context}:");
        }
        else
        {
            lines.Add("Validation warnings:");
        }

        foreach (ValidationWarning warning in Warnings)
        {
            string location = warning.Location != null ? $" (at {warning.Location})" : "";
            lines.Add($"  - {warning.Message}{location}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    ///     Creates a successful validation result.
    /// </summary>
    /// <param name="context">Optional context information</param>
    /// <returns>Valid ValidationResult</returns>
    public static ValidationResult Success(string? context = null)
    {
        return new ValidationResult { Context = context };
    }

    /// <summary>
    ///     Creates a failed validation result with errors.
    /// </summary>
    /// <param name="context">Optional context information</param>
    /// <param name="errors">Error messages</param>
    /// <returns>Invalid ValidationResult</returns>
    public static ValidationResult Failure(string? context, params string[] errors)
    {
        var result = new ValidationResult { Context = context };
        foreach (string error in errors)
        {
            result.AddError(error);
        }

        return result;
    }

    /// <summary>
    ///     Creates a validation result with warnings (still valid).
    /// </summary>
    /// <param name="context">Optional context information</param>
    /// <param name="warnings">Warning messages</param>
    /// <returns>Valid ValidationResult with warnings</returns>
    public static ValidationResult WithWarnings(string? context, params string[] warnings)
    {
        var result = new ValidationResult { Context = context };
        foreach (string warning in warnings)
        {
            result.AddWarning(warning);
        }

        return result;
    }

    /// <summary>
    ///     Merges another validation result into this one.
    ///     Combines errors and warnings from both results.
    /// </summary>
    /// <param name="other">ValidationResult to merge</param>
    public void Merge(ValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
    }

    /// <summary>
    ///     String representation for debugging.
    /// </summary>
    public override string ToString()
    {
        string result = $"Valid: {IsValid}";
        if (!string.IsNullOrEmpty(Context))
        {
            result += $" (Context: {Context})";
        }

        result += "\n";

        if (Errors.Count > 0)
        {
            result += $"Errors ({Errors.Count}):\n{GetErrorMessage()}\n";
        }

        if (Warnings.Count > 0)
        {
            result += $"Warnings ({Warnings.Count}):\n{GetWarningMessage()}\n";
        }

        return result;
    }
}

/// <summary>
///     Represents a validation error that must be fixed.
/// </summary>
/// <param name="Message">Error message</param>
/// <param name="Location">Optional location where the error occurred</param>
public record ValidationError(string Message, string? Location = null);

/// <summary>
///     Represents a validation warning that should be reviewed.
/// </summary>
/// <param name="Message">Warning message</param>
/// <param name="Location">Optional location where the warning occurred</param>
public record ValidationWarning(string Message, string? Location = null);
