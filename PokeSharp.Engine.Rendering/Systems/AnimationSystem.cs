using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Rendering.Animation;
using AnimationComponent = PokeSharp.Game.Components.Rendering.Animation;
using PokeSharp.Engine.Core.Systems;

namespace PokeSharp.Engine.Rendering.Systems;

/// <summary>
///     System that updates sprite animations by modifying sprite source rectangles based on
///     animation definitions and frame timing.
///     Executes at priority 800 (after movement, before rendering).
/// </summary>
public class AnimationSystem : SystemBase, IUpdateSystem
{
    private readonly AnimationLibrary _animationLibrary;
    private readonly ILogger<AnimationSystem>? _logger;
    private ulong _frameCounter;

    /// <summary>
    ///     Creates a new AnimationSystem with the specified animation library and optional logger.
    /// </summary>
    /// <param name="animationLibrary">The animation library containing animation definitions.</param>
    /// <param name="logger">Optional logger for system diagnostics.</param>
    public AnimationSystem(
        AnimationLibrary animationLibrary,
        ILogger<AnimationSystem>? logger = null
    )
    {
        _animationLibrary = animationLibrary ?? throw new ArgumentNullException(nameof(animationLibrary));
        _logger = logger;
    }

    /// <summary>
    /// Gets the priority for execution order. Lower values execute first.
    /// Animation executes at priority 800, after movement (100) and collision (200).
    /// </summary>
    public override int Priority => SystemPriority.Animation;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;

            // Query all entities with Animation + Sprite components
            var query = QueryCache.Get<AnimationComponent, Sprite>();

            world.Query(
                in query,
                (Entity entity, ref AnimationComponent animation, ref Sprite sprite) =>
                {
                    UpdateAnimation(entity, ref animation, ref sprite, deltaTime);
                }
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Error in AnimationSystem.Update (Frame {FrameCounter})",
                _frameCounter
            );
            throw;
        }
    }

    /// <summary>
    ///     Updates a single animation and its corresponding sprite.
    /// </summary>
    private void UpdateAnimation(
        Entity entity,
        ref AnimationComponent animation,
        ref Sprite sprite,
        float deltaTime
    )
    {
        // Skip if animation is not playing
        if (!animation.IsPlaying || animation.IsComplete)
            return;

        // Skip sprite-based animations (handled by SpriteAnimationSystem)
        // These sprites have Category and SpriteName set and use manifest-based animations
        if (!string.IsNullOrEmpty(sprite.Category) && !string.IsNullOrEmpty(sprite.SpriteName))
        {
            return;
        }

        // Get the animation definition
        if (!_animationLibrary.TryGetAnimation(animation.CurrentAnimation, out var animDef))
        {
            _logger?.LogWarning(
                "Animation '{AnimationName}' not found in library",
                animation.CurrentAnimation
            );
            return;
        }

        // Safety check for empty animations
        if (animDef!.FrameCount == 0)
        {
            _logger?.LogWarning(
                "Animation '{AnimationName}' has no frames",
                animation.CurrentAnimation
            );
            return;
        }

        var previousFrame = animation.CurrentFrame;

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
                    // Loop back to first frame - clear triggered events for new loop
                    animation.CurrentFrame = 0;
                    animation.TriggeredEventFrames.Clear();
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

        // Trigger events for the current frame if it changed and hasn't been triggered yet
        if (animation.CurrentFrame != previousFrame)
            TriggerFrameEvents(entity, animation.CurrentFrame, animDef, ref animation);

        // Update sprite source rectangle to current frame
        try
        {
            sprite.SourceRect = animDef.GetFrame(animation.CurrentFrame);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger?.LogError(
                ex,
                "Frame index {FrameIndex} out of range for animation '{AnimationName}'",
                animation.CurrentFrame,
                animation.CurrentAnimation
            );

            // Reset to first frame to recover
            animation.CurrentFrame = 0;
            animation.TriggeredEventFrames.Clear();
            sprite.SourceRect = animDef.GetFrame(0);
        }
    }

    /// <summary>
    ///     Triggers all events associated with a specific frame.
    /// </summary>
    /// <param name="entity">The entity that owns the animation.</param>
    /// <param name="frameIndex">The frame index to check for events.</param>
    /// <param name="animDef">The animation definition containing the events.</param>
    /// <param name="animation">The animation component to track triggered events.</param>
    private void TriggerFrameEvents(
        Entity entity,
        int frameIndex,
        AnimationDefinition animDef,
        ref AnimationComponent animation
    )
    {
        // Check if this frame has events and hasn't been triggered yet
        if (
            !animDef.HasEventsOnFrame(frameIndex)
            || animation.TriggeredEventFrames.Contains(frameIndex)
        )
            return;

        // Get events for this frame
        var events = animDef.GetEventsForFrame(frameIndex);

        // Trigger each event
        foreach (var animEvent in events)
            try
            {
                _logger?.LogDebug(
                    "Triggering animation event '{EventName}' at frame {FrameIndex} for animation '{AnimationName}'",
                    animEvent.EventName,
                    frameIndex,
                    animation.CurrentAnimation
                );

                animEvent.Trigger(entity);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Error triggering animation event '{EventName}' at frame {FrameIndex} for animation '{AnimationName}'",
                    animEvent.EventName,
                    frameIndex,
                    animation.CurrentAnimation
                );
            }

        // Mark this frame as triggered
        animation.TriggeredEventFrames.Add(frameIndex);
    }

}
