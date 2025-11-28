namespace PokeSharp.Game.Data.Configuration;

/// <summary>
///     Configuration options for map loading
/// </summary>
public class MapLoaderOptions
{
    /// <summary>
    ///     Whether to validate maps before loading (default: true)
    /// </summary>
    public bool ValidateMaps { get; set; } = true;

    /// <summary>
    ///     Whether to validate file references in maps (default: true)
    /// </summary>
    public bool ValidateFileReferences { get; set; } = true;

    /// <summary>
    ///     Whether to throw exceptions on validation errors (default: true)
    ///     If false, validation errors will be logged but loading will continue
    /// </summary>
    public bool ThrowOnValidationError { get; set; } = true;

    /// <summary>
    ///     Whether to log validation warnings (default: true)
    /// </summary>
    public bool LogValidationWarnings { get; set; } = true;

    /// <summary>
    ///     Base directory for resolving relative paths in map files
    /// </summary>
    public string? BaseDirectory { get; set; }
}
