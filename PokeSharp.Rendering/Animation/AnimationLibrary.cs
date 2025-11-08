using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace PokeSharp.Rendering.Animation;

/// <summary>
///     Central library for managing and accessing animation definitions.
///     Provides pre-defined animations for common entities like the player character.
/// </summary>
public class AnimationLibrary
{
    private readonly Dictionary<string, AnimationDefinition> _animations = new();
    private readonly ILogger<AnimationLibrary>? _logger;

    /// <summary>
    ///     Initializes a new instance of the AnimationLibrary class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public AnimationLibrary(ILogger<AnimationLibrary>? logger = null)
    {
        _logger = logger;
        InitializeDefaultAnimations();
    }

    /// <summary>
    ///     Gets the total count of registered animations.
    /// </summary>
    public int Count => _animations.Count;

    /// <summary>
    ///     Gets an animation definition by name.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <returns>The animation definition.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the animation is not found.</exception>
    public AnimationDefinition GetAnimation(string name)
    {
        if (!_animations.TryGetValue(name, out var animation))
            throw new KeyNotFoundException($"Animation '{name}' not found in library.");

        return animation;
    }

    /// <summary>
    ///     Checks if an animation exists in the library.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <returns>True if the animation exists; otherwise, false.</returns>
    public bool HasAnimation(string name)
    {
        return _animations.ContainsKey(name);
    }

    /// <summary>
    ///     Tries to get an animation definition by name.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="animation">The animation definition if found.</param>
    /// <returns>True if the animation exists; otherwise, false.</returns>
    public bool TryGetAnimation(string name, out AnimationDefinition? animation)
    {
        return _animations.TryGetValue(name, out animation);
    }

    /// <summary>
    ///     Registers a new animation definition.
    /// </summary>
    /// <param name="animation">The animation to register.</param>
    public void RegisterAnimation(AnimationDefinition animation)
    {
        if (string.IsNullOrWhiteSpace(animation.Name))
            throw new ArgumentException(
                "Animation name cannot be null or empty.",
                nameof(animation)
            );

        _animations[animation.Name] = animation;
        _logger?.LogDebug(
            "Registered animation: '{AnimationName}' with {FrameCount} frames",
            animation.Name,
            animation.FrameCount
        );
    }

    /// <summary>
    ///     Gets all registered animation names.
    /// </summary>
    /// <returns>Collection of animation names.</returns>
    public IReadOnlyCollection<string> GetAnimationNames()
    {
        return _animations.Keys;
    }

    /// <summary>
    ///     Initializes default animations for the player character.
    ///     Assumes a 16x16 sprite sheet with 4 rows (directions) x 4 columns (frames).
    /// </summary>
    private void InitializeDefaultAnimations()
    {
        const int frameWidth = 16;
        const int frameHeight = 16;

        _logger?.LogInformation("Initializing default player animations...");

        // Walk animations (4 frames each)
        RegisterAnimation(
            AnimationDefinition.CreateFromGrid("walk_down", 0, 0, frameWidth, frameHeight, 4)
        );

        RegisterAnimation(
            AnimationDefinition.CreateFromGrid(
                "walk_left",
                0,
                frameHeight,
                frameWidth,
                frameHeight,
                4
            )
        );

        RegisterAnimation(
            AnimationDefinition.CreateFromGrid(
                "walk_right",
                0,
                frameHeight * 2,
                frameWidth,
                frameHeight,
                4
            )
        );

        RegisterAnimation(
            AnimationDefinition.CreateFromGrid(
                "walk_up",
                0,
                frameHeight * 3,
                frameWidth,
                frameHeight,
                4
            )
        );

        // Idle animations (single frame - first frame of each walk cycle)
        RegisterAnimation(
            AnimationDefinition.CreateSingleFrame(
                "idle_down",
                new Rectangle(0, 0, frameWidth, frameHeight)
            )
        );

        RegisterAnimation(
            AnimationDefinition.CreateSingleFrame(
                "idle_left",
                new Rectangle(0, frameHeight, frameWidth, frameHeight)
            )
        );

        RegisterAnimation(
            AnimationDefinition.CreateSingleFrame(
                "idle_right",
                new Rectangle(0, frameHeight * 2, frameWidth, frameHeight)
            )
        );

        RegisterAnimation(
            AnimationDefinition.CreateSingleFrame(
                "idle_up",
                new Rectangle(0, frameHeight * 3, frameWidth, frameHeight)
            )
        );

        _logger?.LogInformation("Registered {Count} default animations", _animations.Count);
    }

    /// <summary>
    ///     Clears all animations from the library.
    /// </summary>
    public void Clear()
    {
        _animations.Clear();
        _logger?.LogDebug("Animation library cleared");
    }
}
