namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Marker interface for events that are associated with a specific tile position.
///     Enables tile-filtered event subscriptions via ScriptBase.OnTile&lt;TEvent&gt;().
/// </summary>
/// <remarks>
///     <para>
///         Events that implement this interface can be filtered by tile coordinates,
///         allowing scripts to subscribe only to events at specific tile positions.
///     </para>
///     <para>
///         Example: Subscribe only to step events on a specific grass tile,
///         ignoring step events on all other tiles in the map.
///     </para>
///     <para>
///         <strong>NOTE:</strong> This is a marker interface introduced in Phase 3.1.
///         Existing events (TileSteppedOnEvent, etc.) have TileX/TileY properties but
///         don't implement this interface yet. Future phases will retrofit these interfaces.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // Event implementation
/// public sealed record CustomTileEvent : ITileEvent
/// {
///     public Guid EventId { get; init; } = Guid.NewGuid();
///     public DateTime Timestamp { get; init; } = DateTime.UtcNow;
///     public required int TileX { get; init; }
///     public required int TileY { get; init; }
///     public string CustomData { get; init; }
/// }
///
/// // Usage in script
/// public class TileScript : ScriptBase
/// {
///     public override void RegisterEventHandlers(ScriptContext ctx)
///     {
///         var tilePos = new Vector2(10, 15);
///
///         // Only receive events for this specific tile
///         OnTile&lt;CustomTileEvent&gt;(tilePos, evt =>
///         {
///             ctx.Logger.LogInformation("Tile event: {Data}", evt.CustomData);
///         });
///     }
/// }
/// </code>
/// </example>
public interface ITileEvent : IGameEvent
{
    /// <summary>
    ///     Gets the X coordinate of the tile associated with this event.
    ///     Used for tile-filtered event subscriptions.
    /// </summary>
    int TileX { get; }

    /// <summary>
    ///     Gets the Y coordinate of the tile associated with this event.
    ///     Used for tile-filtered event subscriptions.
    /// </summary>
    int TileY { get; }
}
