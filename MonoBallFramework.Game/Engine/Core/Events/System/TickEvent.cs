namespace MonoBallFramework.Game.Engine.Core.Events.System;

/// <summary>
///     Event published every frame for time-based updates.
///     Used by scripts that need frame-by-frame logic (NPC AI, animations, etc.)
/// </summary>
/// <remarks>
///     This is a high-frequency event published every frame (60+ times per second).
///     Object pooling is essential for this event to avoid GC pressure.
///     This class supports object pooling via EventPool{T} to reduce allocations.
/// </remarks>
public sealed class TickEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the time elapsed since the last frame (in seconds).
    /// </summary>
    public float DeltaTime { get; set; }

    /// <summary>
    ///     Gets or sets the total elapsed time since game start (in seconds).
    /// </summary>
    public float TotalTime { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        DeltaTime = 0f;
        TotalTime = 0f;
    }
}
