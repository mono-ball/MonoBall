using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Base class for all strongly-typed entity identifiers.
///     Format: {namespace}:{type}:{category}/{name}
///     Or with subcategory: {namespace}:{type}:{category}/{subcategory}/{name}
///     Examples:
///     - base:map:hoenn/littleroot_town
///     - base:npc:townfolk/prof_birch
///     - base:trainer:youngster/joey
///     - base:sprite:npcs/generic/boy_1 (with subcategory)
///     - base:sprite:players/may
/// </summary>
[DebuggerDisplay("{Value}")]
public abstract record EntityId
{
    /// <summary>
    ///     Default namespace for base game content.
    /// </summary>
    public const string BaseNamespace = "base";

    /// <summary>
    ///     Regex pattern for validating entity IDs.
    ///     Format: namespace:type:category/name OR namespace:type:category/subcategory/name
    ///     Allows forward slashes and hyphens in the name part for hierarchical naming.
    /// </summary>
    private static readonly Regex IdPattern = new(
        @"^[a-z0-9_]+:[a-z_]+:[a-z0-9_]+/[a-z0-9_/-]+(/[a-z0-9_]+)?$",
        RegexOptions.Compiled);

    /// <summary>
    ///     Initializes a new entity ID from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:map:hoenn/littleroot_town")</param>
    /// <exception cref="ArgumentException">Thrown when value is invalid.</exception>
    protected EntityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Entity ID cannot be null or whitespace.", nameof(value));
        }

        if (!IdPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Entity ID '{value}' does not match expected format: namespace:type:category/name",
                nameof(value));
        }

        Value = value;
        ParseComponents();
    }

    /// <summary>
    ///     Initializes a new entity ID from components.
    ///     Validates that all components are non-empty after normalization and that the
    ///     constructed ID matches the expected format.
    /// </summary>
    /// <param name="entityType">The entity type (e.g., "map", "npc", "trainer")</param>
    /// <param name="category">The category within the type (e.g., "hoenn", "townfolk")</param>
    /// <param name="name">The specific name (e.g., "littleroot_town", "prof_birch")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory (e.g., "generic" for sprites)</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when any component is empty after normalization or the constructed ID is
    ///     invalid.
    /// </exception>
    protected EntityId(string entityType, string category, string name, string? ns = null, string? subcategory = null)
    {
        Namespace = ns ?? BaseNamespace;
        EntityType = entityType.ToLowerInvariant();
        Category = NormalizeComponent(category);
        Name = NormalizeComponent(name);
        Subcategory = subcategory != null ? NormalizeComponent(subcategory) : null;

        // Validate components are not empty after normalization
        if (string.IsNullOrEmpty(Category))
        {
            throw new ArgumentException("Category cannot be empty after normalization.", nameof(category));
        }

        if (string.IsNullOrEmpty(Name))
        {
            throw new ArgumentException("Name cannot be empty after normalization.", nameof(name));
        }

        if (string.IsNullOrEmpty(EntityType))
        {
            throw new ArgumentException("Entity type cannot be empty.", nameof(entityType));
        }

        if (subcategory != null && string.IsNullOrEmpty(Subcategory))
        {
            throw new ArgumentException("Subcategory cannot be empty after normalization when provided.",
                nameof(subcategory));
        }

        // Build value with or without subcategory
        Value = Subcategory != null
            ? $"{Namespace}:{EntityType}:{Category}/{Subcategory}/{Name}"
            : $"{Namespace}:{EntityType}:{Category}/{Name}";

        // Final validation against regex pattern
        if (!IdPattern.IsMatch(Value))
        {
            throw new ArgumentException(
                $"Constructed entity ID '{Value}' does not match expected format: namespace:type:category/[subcategory/]name",
                nameof(name));
        }
    }

    /// <summary>
    ///     The full ID string.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///     The namespace (e.g., "base", "mymod").
    /// </summary>
    public string Namespace { get; private set; } = BaseNamespace;

    /// <summary>
    ///     The entity type (e.g., "map", "npc", "trainer").
    /// </summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>
    ///     The category within the type (e.g., "hoenn", "townfolk", "npcs").
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    ///     The optional subcategory (e.g., "generic" for generic NPCs/sprites).
    ///     Null if no subcategory is present.
    /// </summary>
    public string? Subcategory { get; private set; }

    /// <summary>
    ///     The specific name (e.g., "littleroot_town", "prof_birch", "boy_1").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    ///     Returns just the local part (category/[subcategory/]name) without namespace and type.
    /// </summary>
    public string LocalId => Subcategory != null
        ? $"{Category}/{Subcategory}/{Name}"
        : $"{Category}/{Name}";

    /// <summary>
    ///     Whether this ID has a subcategory.
    /// </summary>
    public bool HasSubcategory => Subcategory != null;

    /// <summary>
    ///     Returns the path part (type:category/name) without namespace.
    /// </summary>
    public string Path => $"{EntityType}:{LocalId}";

    /// <summary>
    ///     Whether this ID is from the base game (namespace = "base").
    /// </summary>
    public bool IsBaseGame => Namespace == BaseNamespace;

    /// <summary>
    ///     Parse the Value string into components.
    /// </summary>
    private void ParseComponents()
    {
        // Format: namespace:type:category/name OR namespace:type:category/subcategory/name
        int firstColon = Value.IndexOf(':');
        int secondColon = Value.IndexOf(':', firstColon + 1);
        int firstSlash = Value.IndexOf('/');
        int secondSlash = Value.IndexOf('/', firstSlash + 1);

        Namespace = Value[..firstColon];
        EntityType = Value[(firstColon + 1)..secondColon];
        Category = Value[(secondColon + 1)..firstSlash];

        if (secondSlash > 0)
        {
            // Has subcategory: category/subcategory/name
            Subcategory = Value[(firstSlash + 1)..secondSlash];
            Name = Value[(secondSlash + 1)..];
        }
        else
        {
            // No subcategory: category/name
            Subcategory = null;
            Name = Value[(firstSlash + 1)..];
        }
    }

    /// <summary>
    ///     Normalize a component string to lowercase with underscores.
    ///     Optimized single-pass algorithm using Span to minimize allocations.
    /// </summary>
    protected static string NormalizeComponent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Single-pass normalization with stackalloc
        Span<char> buffer = stackalloc char[value.Length];
        int writeIndex = 0;
        bool lastWasUnderscore = true; // Start true to trim leading underscores

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c) || c == '-')
            {
                // Convert spaces and hyphens to underscore (collapse multiples)
                if (!lastWasUnderscore && writeIndex > 0)
                {
                    buffer[writeIndex++] = '_';
                    lastWasUnderscore = true;
                }
            }
            else if (c >= 'A' && c <= 'Z')
            {
                // Convert uppercase to lowercase
                buffer[writeIndex++] = (char)(c + 32);
                lastWasUnderscore = false;
            }
            else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                // Keep lowercase letters and digits
                buffer[writeIndex++] = c;
                lastWasUnderscore = false;
            }
            else if (c == '_')
            {
                // Keep underscore (but collapse multiples)
                if (!lastWasUnderscore)
                {
                    buffer[writeIndex++] = '_';
                    lastWasUnderscore = true;
                }
            }
            // Skip all other characters (invalid chars removed)
        }

        // Trim trailing underscore
        if (writeIndex > 0 && buffer[writeIndex - 1] == '_')
        {
            writeIndex--;
        }

        return writeIndex > 0 ? new string(buffer[..writeIndex]) : string.Empty;
    }

    /// <summary>
    ///     Validates that an ID string matches the expected format.
    /// </summary>
    public static bool IsValidFormat(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && IdPattern.IsMatch(value);
    }

    /// <summary>
    ///     Returns the string representation of the ID.
    /// </summary>
    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    ///     Implicit conversion to string.
    /// </summary>
    public static implicit operator string(EntityId id)
    {
        return id.Value;
    }
}
