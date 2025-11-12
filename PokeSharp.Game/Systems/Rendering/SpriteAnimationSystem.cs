using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Services;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that updates sprite frames based on animation data.
///     Reads animation sequences from NPCSpriteLoader manifests and updates
///     the Sprite component's CurrentFrame and SourceRect properties.
///     Priority: 875 (after Movement:700 and TileAnimation:850, before Render:1000).
/// </summary>
public class SpriteAnimationSystem : SystemBase, IUpdateSystem
{
    private readonly SpriteLoader _spriteLoader;
    private readonly ILogger<SpriteAnimationSystem>? _logger;

    // Cache manifests for performance (avoid repeated async loads)
    private readonly Dictionary<string, SpriteManifest> _manifestCache = new();

    public SpriteAnimationSystem(
        SpriteLoader spriteLoader,
        ILogger<SpriteAnimationSystem>? logger = null)
    {
        _spriteLoader = spriteLoader ?? throw new ArgumentNullException(nameof(spriteLoader));
        _logger = logger;
    }

    /// <summary>
    /// Gets the priority for execution order. Lower values execute first.
    /// Sprite animation executes at priority 875, after tile animation (850).
    /// </summary>
    public override int Priority => SystemPriority.SpriteAnimation;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (!Enabled)
            return;

        // Query all entities with Position, Sprite + Animation components
        world.Query(
            in EcsQueries.AnimatedSprites,
            (Entity entity, ref Position position, ref Sprite sprite, ref Animation anim) =>
            {
                // Copy animation to avoid ref parameter in lambda
                var animCopy = anim;
                UpdateSpriteAnimation(ref sprite, ref animCopy, deltaTime);
                anim = animCopy;
            }
        );
    }

    /// <summary>
    ///     Updates a single sprite's animation frame based on time and animation data.
    /// </summary>
    private void UpdateSpriteAnimation(
        ref Sprite sprite,
        ref Animation animation,
        float deltaTime)
    {
        if (!animation.IsPlaying)
            return;

        // Get or load manifest for this sprite
        var manifestKey = $"{sprite.Category}/{sprite.SpriteName}";
        if (!_manifestCache.TryGetValue(manifestKey, out var manifest))
        {
            try
            {
                // Load manifest synchronously (cached by NPCSpriteLoader internally)
                // LoadSpriteAsync searches by sprite name across all categories
                manifest = _spriteLoader.LoadSpriteAsync(sprite.SpriteName).Result;

                if (manifest == null)
                {
                    _logger?.LogWarning(
                        "Sprite manifest not found for {SpriteName}",
                        sprite.SpriteName);
                    return;
                }

                _manifestCache[manifestKey] = manifest;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Failed to load sprite manifest for {Category}/{SpriteName}",
                    sprite.Category,
                    sprite.SpriteName);
                return;
            }
        }

        // Find the current animation in the manifest
        var currentAnimName = animation.CurrentAnimation;
        var animData = manifest.Animations
            .FirstOrDefault(a => a.Name == currentAnimName);

        if (animData == null)
        {
            _logger?.LogWarning(
                "Animation '{AnimationName}' not found in sprite {Category}/{SpriteName}",
                animation.CurrentAnimation,
                sprite.Category,
                sprite.SpriteName);
            return;
        }

        // Set flip from animation data
        sprite.FlipHorizontal = animData.FlipHorizontal;

        // Validate animation has frames
        if (animData.FrameIndices == null || animData.FrameIndices.Length == 0)
            return;

        // Update frame timer
        animation.FrameTimer += deltaTime;

        // Check if we need to advance to next frame
        if (animation.FrameTimer >= animData.FrameDuration)
        {
            // Advance to next frame in the animation sequence
            animation.CurrentFrame++;

            // Handle looping
            if (animation.CurrentFrame >= animData.FrameIndices.Length)
            {
                if (animData.Loop)
                {
                    animation.CurrentFrame = 0;
                    animation.TriggeredEventFrames.Clear(); // Reset event triggers on loop
                }
                else
                {
                    // Non-looping animation completed
                    animation.CurrentFrame = animData.FrameIndices.Length - 1;
                    animation.IsComplete = true;
                    animation.IsPlaying = false;
                }
            }

            // Reset frame timer
            animation.FrameTimer = 0f;
        }

        // Update sprite's current frame index from animation sequence
        var frameIndexInSequence = animation.CurrentFrame % animData.FrameIndices.Length;
        var frameIndexInSpriteSheet = animData.FrameIndices[frameIndexInSequence];
        sprite.CurrentFrame = frameIndexInSpriteSheet;

        // Update source rectangle from frame data
        if (frameIndexInSpriteSheet >= 0 && frameIndexInSpriteSheet < manifest.Frames.Count)
        {
            var frame = manifest.Frames[frameIndexInSpriteSheet];
            sprite.SourceRect = new Rectangle(frame.X, frame.Y, frame.Width, frame.Height);

            // Set origin to bottom-left for grid alignment
            // This makes the sprite's bottom-left align with the tile's bottom-left
            sprite.Origin = new Vector2(0, frame.Height);
        }
    }
}

