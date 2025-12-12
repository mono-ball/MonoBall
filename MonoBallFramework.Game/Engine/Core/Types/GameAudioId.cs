using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for audio definitions (music and sound effects).
///
///     Format: base:audio:{category}/{subcategory}/{name}
///     Examples:
///     - base:audio:music/towns/mus_littleroot
///     - base:audio:music/battle/mus_vs_wild
///     - base:audio:sfx/battle/se_select
///     - mymod:audio:music/custom/my_theme
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameAudioId : EntityId
{
    private const string TypeName = "audio";
    private const string DefaultCategory = "music";

    /// <summary>
    ///     Initializes a new GameAudioId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:audio:music/towns/mus_littleroot")</param>
    public GameAudioId(string value) : base(value)
    {
        if (EntityType != TypeName)
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
    }

    /// <summary>
    ///     Initializes a new GameAudioId from components.
    /// </summary>
    /// <param name="category">The audio category (e.g., "music", "sfx")</param>
    /// <param name="name">The audio name (e.g., "mus_littleroot")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">The subcategory (e.g., "towns", "battle", "routes")</param>
    public GameAudioId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Constructs a GameAudioId from audio name and category components.
    ///     Use this when you have the individual components, not a formatted string.
    /// </summary>
    /// <param name="audioName">The audio name (e.g., "mus_littleroot")</param>
    /// <param name="category">Optional category (defaults to "music")</param>
    /// <param name="subcategory">Optional subcategory (e.g., "towns")</param>
    /// <returns>A new GameAudioId</returns>
    public static GameAudioId FromComponents(string audioName, string? category = null, string? subcategory = null)
    {
        return new GameAudioId(category ?? DefaultCategory, audioName, subcategory: subcategory);
    }

    /// <summary>
    ///     Tries to parse a GameAudioId from a string, returning null if invalid.
    ///     Only accepts the full format: base:audio:{category}/{subcategory}/{name}
    /// </summary>
    /// <param name="value">The ID string to parse</param>
    public static GameAudioId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Only accept full format
        if (!IsValidFormat(value) || !value.Contains($":{TypeName}:"))
            return null;

        try
        {
            return new GameAudioId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Returns true if this is a music track.
    /// </summary>
    public bool IsMusic => Category == "music";

    /// <summary>
    ///     Returns true if this is a sound effect.
    /// </summary>
    public bool IsSoundEffect => Category == "sfx";

    /// <summary>
    ///     Returns the audio subcategory (e.g., "towns", "battle", "routes").
    /// </summary>
    public string? AudioSubcategory => Subcategory;

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameAudioId(string value) => new(value);
}
