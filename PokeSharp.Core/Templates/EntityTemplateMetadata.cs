namespace PokeSharp.Core.Templates;

/// <summary>
///     Metadata about an entity template's origin and compilation.
///     Tracks data layer linkage and versioning for hot-reload support.
/// </summary>
public sealed class EntityTemplateMetadata
{
    /// <summary>
    ///     Foreign key to the data layer entity that generated this template.
    ///     Used for tracking and invalidation when source data changes.
    /// </summary>
    public int DataLayerId { get; set; }

    /// <summary>
    ///     Timestamp when this template was compiled.
    ///     Used for hot-reload detection and cache invalidation.
    /// </summary>
    public DateTime CompiledAt { get; set; }

    /// <summary>
    ///     Template version string for compatibility tracking.
    ///     Format: "major.minor.patch" (semantic versioning)
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    ///     Optional source file path if template was loaded from a file.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    ///     Hash of the source data for change detection.
    ///     Used to determine if recompilation is needed.
    /// </summary>
    public string? SourceHash { get; set; }
}
