using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for trainer definitions.
///
///     Format: base:trainer:{class}/{name}
///     Examples:
///     - base:trainer:youngster/joey
///     - base:trainer:gym_leader/roxanne
///     - base:trainer:elite_four/sidney
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameTrainerId : EntityId
{
    private const string TypeName = "trainer";
    private const string DefaultClass = "trainer";

    /// <summary>
    ///     Initializes a new GameTrainerId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:trainer:youngster/joey")</param>
    public GameTrainerId(string value) : base(value)
    {
        if (EntityType != TypeName)
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
    }

    /// <summary>
    ///     Initializes a new GameTrainerId from components.
    /// </summary>
    /// <param name="trainerClass">The trainer class (e.g., "youngster", "gym_leader")</param>
    /// <param name="name">The trainer name (e.g., "joey", "roxanne")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameTrainerId(string trainerClass, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, trainerClass, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Creates a GameTrainerId from just a name, using defaults.
    /// </summary>
    /// <param name="trainerName">The trainer name</param>
    /// <param name="trainerClass">Optional class (defaults to "trainer")</param>
    /// <returns>A new GameTrainerId</returns>
    public static GameTrainerId Create(string trainerName, string? trainerClass = null)
    {
        return new GameTrainerId(trainerClass ?? DefaultClass, trainerName);
    }

    /// <summary>
    ///     Creates a gym leader trainer ID.
    /// </summary>
    /// <param name="name">The trainer name (e.g., "roxanne", "brawly")</param>
    /// <returns>A new GameTrainerId with "gym_leader" class</returns>
    public static GameTrainerId CreateGymLeader(string name)
    {
        return new GameTrainerId("gym_leader", name);
    }

    /// <summary>
    ///     Creates an Elite Four trainer ID.
    /// </summary>
    /// <param name="name">The trainer name (e.g., "sidney", "phoebe")</param>
    /// <returns>A new GameTrainerId with "elite_four" class</returns>
    public static GameTrainerId CreateEliteFour(string name)
    {
        return new GameTrainerId("elite_four", name);
    }

    /// <summary>
    ///     Creates a youngster trainer ID.
    /// </summary>
    /// <param name="name">The trainer name (e.g., "joey", "calvin")</param>
    /// <returns>A new GameTrainerId with "youngster" class</returns>
    public static GameTrainerId CreateYoungster(string name)
    {
        return new GameTrainerId("youngster", name);
    }

    /// <summary>
    ///     Tries to create a GameTrainerId from a string, returning null if invalid.
    ///     Only accepts the full format: base:trainer:{class}/{name}
    /// </summary>
    public static GameTrainerId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Only accept full format
        if (!IsValidFormat(value) || !value.Contains($":{TypeName}:"))
            return null;

        try
        {
            return new GameTrainerId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameTrainerId(string value) => new(value);
}
