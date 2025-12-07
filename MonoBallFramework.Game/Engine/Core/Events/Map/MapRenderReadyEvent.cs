namespace MonoBallFramework.Game.Engine.Core.Events.Map;

/// <summary>
///     Event published after the map has finished loading and the first frame has been rendered.
///     This is the ideal time to show map popups, as the player can now see the map.
/// </summary>
public sealed class MapRenderReadyEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the ID of the loaded map.
    /// </summary>
    public string MapId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the name of the loaded map.
    /// </summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the region/section name for the map (e.g., "Route 101", "Littleroot Town").
    ///     Used for displaying the map popup banner.
    /// </summary>
    public string? RegionName { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        MapId = string.Empty;
        MapName = string.Empty;
        RegionName = null;
    }
}



