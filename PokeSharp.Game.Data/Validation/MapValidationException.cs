namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Exception thrown when map validation fails
/// </summary>
public class MapValidationException : Exception
{
    public MapValidationException(ValidationResult validationResult)
        : base(validationResult.GetErrorMessage())
    {
        ValidationResult = validationResult;
    }

    public MapValidationException(ValidationResult validationResult, string message)
        : base(message)
    {
        ValidationResult = validationResult;
    }

    public MapValidationException(
        ValidationResult validationResult,
        string message,
        Exception innerException
    )
        : base(message, innerException)
    {
        ValidationResult = validationResult;
    }

    /// <summary>
    ///     The validation result containing all errors and warnings
    /// </summary>
    public ValidationResult ValidationResult { get; }
}
