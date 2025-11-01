using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Animation;
using AnimationComponent = PokeSharp.Core.Components.Animation;

namespace PokeSharp.Rendering.Systems;

/// <summary>
/// System that updates sprite animations by modifying sprite source rectangles based on
/// animation definitions and frame timing.
/// Executes at priority 800 (after movement, before rendering).
/// </summary>
public class AnimationSystem : BaseSystem
{
    private readonly AnimationLibrary _animationLibrary;
    private readonly ILogger<AnimationSystem>? _logger;
    private ulong _frameCounter = 0;

    /// <summary>
    /// Initializes a new instance of the AnimationSystem class.
    /// </summary>
    /// <param name="animationLibrary">The animation library containing all animation definitions.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public AnimationSystem(AnimationLibrary animationLibrary, ILogger<AnimationSystem>? logger = null)
    {
        _animationLibrary = animationLibrary ?? throw new ArgumentNullException(nameof(animationLibrary));
        _logger = logger;
    }

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Animation;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;

            // Query all entities with Animation + Sprite components
            var query = new QueryDescription().WithAll<AnimationComponent, Sprite>();

            world.Query(in query, (ref AnimationComponent animation, ref Sprite sprite) =>
            {
                UpdateAnimation(ref animation, ref sprite, deltaTime);
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in AnimationSystem.Update (Frame {FrameCounter})", _frameCounter);
            throw;
        }
    }

    /// <summary>
    /// Updates a single animation and its corresponding sprite.
    /// </summary>
    private void UpdateAnimation(ref AnimationComponent animation, ref Sprite sprite, float deltaTime)
    {
        // Skip if animation is not playing
        if (!animation.IsPlaying || animation.IsComplete)
        {
            return;
        }

        // Get the animation definition
        if (!_animationLibrary.TryGetAnimation(animation.CurrentAnimation, out var animDef))
        {
            _logger?.LogWarning("Animation '{AnimationName}' not found in library", animation.CurrentAnimation);
            return;
        }

        // Safety check for empty animations
        if (animDef!.FrameCount == 0)
        {
            _logger?.LogWarning("Animation '{AnimationName}' has no frames", animation.CurrentAnimation);
            return;
        }

        // Update frame timer
        animation.FrameTimer += deltaTime;

        // Check if it's time to advance to the next frame
        if (animation.FrameTimer >= animDef.FrameDuration)
        {
            // Reset timer
            animation.FrameTimer -= animDef.FrameDuration;

            // Advance to next frame
            animation.CurrentFrame++;

            // Handle end of animation
            if (animation.CurrentFrame >= animDef.FrameCount)
            {
                if (animDef.Loop)
                {
                    // Loop back to first frame
                    animation.CurrentFrame = 0;
                }
                else
                {
                    // Animation complete - stay on last frame
                    animation.CurrentFrame = animDef.FrameCount - 1;
                    animation.IsComplete = true;
                    animation.IsPlaying = false;
                }
            }
        }

        // Update sprite source rectangle to current frame
        try
        {
            sprite.SourceRect = animDef.GetFrame(animation.CurrentFrame);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger?.LogError(ex, "Frame index {FrameIndex} out of range for animation '{AnimationName}'",
                animation.CurrentFrame, animation.CurrentAnimation);

            // Reset to first frame to recover
            animation.CurrentFrame = 0;
            sprite.SourceRect = animDef.GetFrame(0);
        }
    }
}
