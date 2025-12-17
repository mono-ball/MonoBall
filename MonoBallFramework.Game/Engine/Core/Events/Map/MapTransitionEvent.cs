using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Core.Events.Map;

/// <summary>
///     Event published when transitioning between maps.
///     This is a notification event (not cancellable) published after the transition is confirmed.
/// </summary>
public sealed class MapTransitionEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the ID of the map being transitioned from.
    ///     Null if this is the initial map load.
    /// </summary>
    public GameMapId? FromMapId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the map being transitioned from.
    ///     Null if this is the initial map load.
    /// </summary>
    public string? FromMapName { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the map being transitioned to.
    /// </summary>
    public GameMapId? ToMapId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the map being transitioned to.
    /// </summary>
    public string ToMapName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the region/section name for the destination map (e.g., "Route 101", "Littleroot Town").
    ///     Used for displaying the map popup banner.
    /// </summary>
    public string? RegionName { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this is the initial map load (no previous map).
    /// </summary>
    public bool IsInitialLoad => FromMapId == null;

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        FromMapId = null;
        FromMapName = null;
        ToMapId = null;
        ToMapName = string.Empty;
        RegionName = null;
    }
}
