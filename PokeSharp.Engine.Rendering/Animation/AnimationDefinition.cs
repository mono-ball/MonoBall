using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Rendering.Animation;

/// <summary>
///     Defines a single animation sequence with frames, durations, and playback settings.
/// </summary>
public class AnimationDefinition
{
    /// <summary>
    ///     Initializes a new instance of the AnimationDefinition class.
    /// </summary>
    public AnimationDefinition() { }

    /// <summary>
    ///     Initializes a new instance of the AnimationDefinition class with specified parameters.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="frames">The frame source rectangles.</param>
    /// <param name="frameDuration">Duration of each frame in seconds.</param>
    /// <param name="loop">Whether the animation loops.</param>
    public AnimationDefinition(
        string name,
        Rectangle[] frames,
        float frameDuration = 0.15f,
        bool loop = true
    )
    {
        Name = name;
        Frames = frames;
        FrameDuration = frameDuration;
        Loop = loop;
    }

    /// <summary>
    ///     Gets or sets the unique identifier for this animation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the array of source rectangles for each frame.
    ///     Each rectangle defines the sprite sheet region for that frame.
    /// </summary>
    public Rectangle[] Frames { get; set; } = [];

    /// <summary>
    ///     Gets or sets the duration of each frame in seconds.
    ///     Used when FrameDurations is null or empty (uniform duration for all frames).
    /// </summary>
    public float FrameDuration { get; set; } = 0.15f; // Default: 6.67 FPS

    /// <summary>
    ///     Gets or sets per-frame durations in seconds (one per frame in Frames array).
    ///     If null or empty, FrameDuration is used for all frames.
    /// </summary>
    public float[]? FrameDurations { get; set; }

    /// <summary>
    ///     Gets or sets whether the animation loops continuously.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    ///     Gets or sets the events that trigger at specific frames.
    ///     Key is the frame index (0-based), value is the list of events for that frame.
    /// </summary>
    public Dictionary<int, List<AnimationEvent>> Events { get; set; } = new();

    /// <summary>
    ///     Gets the total number of frames in this animation.
    /// </summary>
    public int FrameCount => Frames.Length;

    /// <summary>
    ///     Gets the total duration of the animation in seconds.
    ///     Uses per-frame durations if available, otherwise FrameDuration * FrameCount.
    /// </summary>
    public float TotalDuration
    {
        get
        {
            if (FrameDurations != null && FrameDurations.Length > 0)
            {
                return FrameDurations.Sum();
            }

            return FrameCount * FrameDuration;
        }
    }

    /// <summary>
    ///     Gets the source rectangle for a specific frame index.
    /// </summary>
    /// <param name="frameIndex">The frame index (0-based).</param>
    /// <returns>The source rectangle for the frame.</returns>
    public Rectangle GetFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= FrameCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                $"Frame index {frameIndex} is out of range. Valid range: 0-{FrameCount - 1}"
            );
        }

        return Frames[frameIndex];
    }

    /// <summary>
    ///     Creates a simple single-frame animation (for idle poses).
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="frame">The single frame rectangle.</param>
    /// <returns>A new AnimationDefinition with one frame.</returns>
    public static AnimationDefinition CreateSingleFrame(string name, Rectangle frame)
    {
        return new AnimationDefinition(name, new[] { frame }, 1.0f);
    }

    /// <summary>
    ///     Adds an event to trigger at a specific frame.
    /// </summary>
    /// <param name="frameIndex">The frame index (0-based) to trigger the event.</param>
    /// <param name="animationEvent">The event to trigger.</param>
    /// <returns>This AnimationDefinition for method chaining.</returns>
    public AnimationDefinition AddEvent(int frameIndex, AnimationEvent animationEvent)
    {
        if (frameIndex < 0 || frameIndex >= FrameCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                $"Frame index {frameIndex} is out of range. Valid range: 0-{FrameCount - 1}"
            );
        }

        if (!Events.ContainsKey(frameIndex))
        {
            Events[frameIndex] = new List<AnimationEvent>();
        }

        Events[frameIndex].Add(animationEvent);
        return this;
    }

    /// <summary>
    ///     Adds an event to trigger at a specific frame (convenience overload).
    /// </summary>
    /// <param name="frameIndex">The frame index (0-based) to trigger the event.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="callback">The callback to execute.</param>
    /// <returns>This AnimationDefinition for method chaining.</returns>
    public AnimationDefinition AddEvent(int frameIndex, string eventName, Action<Entity> callback)
    {
        return AddEvent(frameIndex, new AnimationEvent(eventName, callback));
    }

    /// <summary>
    ///     Gets all events for a specific frame.
    /// </summary>
    /// <param name="frameIndex">The frame index to query.</param>
    /// <returns>List of events for that frame, or empty list if none.</returns>
    public List<AnimationEvent> GetEventsForFrame(int frameIndex)
    {
        return Events.TryGetValue(frameIndex, out List<AnimationEvent>? events)
            ? events
            : new List<AnimationEvent>();
    }

    /// <summary>
    ///     Checks if a specific frame has any events.
    /// </summary>
    /// <param name="frameIndex">The frame index to check.</param>
    /// <returns>True if the frame has events; otherwise, false.</returns>
    public bool HasEventsOnFrame(int frameIndex)
    {
        return Events.ContainsKey(frameIndex) && Events[frameIndex].Count > 0;
    }

    /// <summary>
    ///     Clears all events from this animation.
    /// </summary>
    /// <returns>This AnimationDefinition for method chaining.</returns>
    public AnimationDefinition ClearEvents()
    {
        Events.Clear();
        return this;
    }

    /// <summary>
    ///     Creates an animation from a sprite sheet grid layout.
    /// </summary>
    /// <param name="name">The animation name.</param>
    /// <param name="startX">Starting X position on sprite sheet.</param>
    /// <param name="startY">Starting Y position on sprite sheet.</param>
    /// <param name="frameWidth">Width of each frame in pixels.</param>
    /// <param name="frameHeight">Height of each frame in pixels.</param>
    /// <param name="frameCount">Number of frames in the sequence.</param>
    /// <param name="frameDuration">Duration of each frame in seconds.</param>
    /// <param name="loop">Whether the animation loops.</param>
    /// <returns>A new AnimationDefinition with calculated frame rectangles.</returns>
    public static AnimationDefinition CreateFromGrid(
        string name,
        int startX,
        int startY,
        int frameWidth,
        int frameHeight,
        int frameCount,
        float frameDuration = 0.15f,
        bool loop = true
    )
    {
        var frames = new Rectangle[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = new Rectangle(startX + (i * frameWidth), startY, frameWidth, frameHeight);
        }

        return new AnimationDefinition(name, frames, frameDuration, loop);
    }
}
