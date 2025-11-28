using System.Text.Json;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
///     Represents a JSON Patch operation targeting a specific data file or type.
///     Uses RFC 6902 JSON Patch format: https://jsonpatch.com/
/// </summary>
public sealed class ModPatch
{
    /// <summary>
    ///     Target file or type to patch (e.g. "Templates/NPCs/guard.json" or "NPCs/guard")
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    ///     JSON Patch operations to apply
    /// </summary>
    public required List<PatchOperation> Operations { get; init; }

    /// <summary>
    ///     Optional description of what this patch does
    /// </summary>
    public string Description { get; init; } = "";
}

/// <summary>
///     A single JSON Patch operation (add, remove, replace, move, copy, test)
/// </summary>
public sealed class PatchOperation
{
    /// <summary>
    ///     Operation type: "add", "remove", "replace", "move", "copy", "test"
    /// </summary>
    public required string Op { get; init; }

    /// <summary>
    ///     JSON Pointer path to the target location (e.g. "/components/0/data/tilesPerSecond")
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///     Value to add/replace (for add, replace, test operations)
    /// </summary>
    public JsonElement? Value { get; init; }

    /// <summary>
    ///     Source path (for move and copy operations)
    /// </summary>
    public string? From { get; init; }

    public void Validate()
    {
        string[] validOps = new[] { "add", "remove", "replace", "move", "copy", "test" };
        if (!validOps.Contains(Op.ToLowerInvariant()))
        {
            throw new InvalidOperationException(
                $"Invalid patch operation: {Op}. Must be one of: {string.Join(", ", validOps)}"
            );
        }

        if (string.IsNullOrWhiteSpace(Path))
        {
            throw new InvalidOperationException("Path is required for all operations");
        }

        // Validate path format (must start with /)
        if (!Path.StartsWith('/'))
        {
            throw new InvalidOperationException($"Path must start with '/': {Path}");
        }

        // Validate operation-specific requirements
        switch (Op.ToLowerInvariant())
        {
            case "add":
            case "replace":
            case "test":
                if (Value == null)
                {
                    throw new InvalidOperationException($"{Op} operation requires 'value'");
                }

                break;

            case "move":
            case "copy":
                if (string.IsNullOrWhiteSpace(From))
                {
                    throw new InvalidOperationException($"{Op} operation requires 'from'");
                }

                break;

            case "remove":
                // No additional validation needed
                break;
        }
    }

    public override string ToString()
    {
        return $"{Op} {Path}";
    }
}
