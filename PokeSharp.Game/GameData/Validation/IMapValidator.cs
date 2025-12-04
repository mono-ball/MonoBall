using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Interface for validating Tiled map data
/// </summary>
public interface IMapValidator
{
    /// <summary>
    ///     Validates a TMX document
    /// </summary>
    /// <param name="map">The TMX document to validate</param>
    /// <param name="mapPath">Path to the map file (for relative path validation)</param>
    /// <returns>Validation result containing errors and warnings</returns>
    ValidationResult Validate(TmxDocument map, string mapPath);
}

/// <summary>
///     Result of map validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    ///     Whether the validation passed (no errors)
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    ///     List of validation errors (must be fixed)
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    ///     List of validation warnings (should be reviewed)
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    ///     Adds an error to the validation result
    /// </summary>
    public void AddError(string message, string? location = null)
    {
        Errors.Add(new ValidationError(message, location));
    }

    /// <summary>
    ///     Adds a warning to the validation result
    /// </summary>
    public void AddWarning(string message, string? location = null)
    {
        Warnings.Add(new ValidationWarning(message, location));
    }

    /// <summary>
    ///     Gets a formatted error message for all errors
    /// </summary>
    public string GetErrorMessage()
    {
        if (IsValid)
        {
            return string.Empty;
        }

        var lines = new List<string> { "Map validation failed:" };

        foreach (ValidationError error in Errors)
        {
            string location = error.Location != null ? $" (at {error.Location})" : "";
            lines.Add($"  - {error.Message}{location}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    ///     Gets a formatted warning message for all warnings
    /// </summary>
    public string GetWarningMessage()
    {
        if (Warnings.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string> { "Map validation warnings:" };

        foreach (ValidationWarning warning in Warnings)
        {
            string location = warning.Location != null ? $" (at {warning.Location})" : "";
            lines.Add($"  - {warning.Message}{location}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    ///     Legacy ToString for backwards compatibility
    /// </summary>
    public override string ToString()
    {
        string result = $"Valid: {IsValid}\n";
        if (Errors.Count > 0)
        {
            result += $"Errors ({Errors.Count}):\n" + GetErrorMessage() + "\n";
        }

        if (Warnings.Count > 0)
        {
            result += $"Warnings ({Warnings.Count}):\n" + GetWarningMessage() + "\n";
        }

        return result;
    }
}

/// <summary>
///     Represents a validation error that must be fixed
/// </summary>
/// <param name="Message">Error message</param>
/// <param name="Location">Optional location where the error occurred</param>
public record ValidationError(string Message, string? Location = null);

/// <summary>
///     Represents a validation warning that should be reviewed
/// </summary>
/// <param name="Message">Warning message</param>
/// <param name="Location">Optional location where the warning occurred</param>
public record ValidationWarning(string Message, string? Location = null);
