using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Core.Events.Flags;

/// <summary>
///     Event raised when a game flag value changes.
///     Systems can subscribe to react to flag changes.
/// </summary>
/// <remarks>
///     This is a notification event (cannot be cancelled) because
///     the flag change has already occurred when the event is published.
///     Use this for:
///     - NPC visibility updates
///     - Story progression triggers
///     - Achievement tracking
///     - Save state marking
/// </remarks>
public sealed class FlagChangedEvent : NotificationEventBase
{
    /// <summary>
    ///     The flag identifier that changed.
    /// </summary>
    public GameFlagId? FlagId { get; set; }

    /// <summary>
    ///     The previous value of the flag.
    /// </summary>
    public bool OldValue { get; set; }

    /// <summary>
    ///     The new value of the flag.
    /// </summary>
    public bool NewValue { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        FlagId = null;
        OldValue = false;
        NewValue = false;
    }
}
