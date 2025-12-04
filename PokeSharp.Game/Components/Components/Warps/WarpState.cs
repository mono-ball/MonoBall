namespace PokeSharp.Game.Components.Warps;

/// <summary>
///     Component tracking player's warp state. Attached to the player entity.
///     Provides ECS-based state management for warp transitions.
/// </summary>
/// <remarks>
///     <para>
///         This component replaces the mutable system state previously tracked
///         in WarpSystem. By storing warp state on the player entity:
///         - State is serializable for save/load
///         - State is queryable and debuggable
///         - State follows ECS data flow patterns
///     </para>
///     <para>
///         <b>Usage:</b> WarpSystem detects warp tiles and sets PendingWarp.
///         WarpExecutionSystem processes PendingWarp and handles async map loading.
///         LastDestination prevents immediate re-warping when landing on a warp tile.
///     </para>
/// </remarks>
public struct WarpState
{
    /// <summary>
    ///     Active warp request waiting to be processed.
    ///     Set by WarpSystem when player steps on a warp tile.
    ///     Cleared by WarpExecutionSystem after processing.
    /// </summary>
    public WarpRequest? PendingWarp { get; set; }

    /// <summary>
    ///     Last warp destination to prevent immediate re-warping.
    ///     When player warps to a tile that also has a warp, this prevents
    ///     the player from immediately warping back.
    /// </summary>
    public WarpDestination? LastDestination { get; set; }

    /// <summary>
    ///     Whether the player is currently in a warp transition.
    ///     While true, movement should be locked and no new warps should trigger.
    /// </summary>
    public bool IsWarping { get; set; }

    /// <summary>
    ///     Game time when warp was initiated.
    ///     Used for timeout detection and warp animation timing.
    /// </summary>
    public float WarpStartTime { get; set; }

    /// <summary>
    ///     Creates a default WarpState with no active warp.
    /// </summary>
    public static WarpState Default =>
        new()
        {
            PendingWarp = null,
            LastDestination = null,
            IsWarping = false,
            WarpStartTime = 0f,
        };

    /// <summary>
    ///     Clears the warp state, resetting all tracking.
    /// </summary>
    public void Clear()
    {
        PendingWarp = null;
        IsWarping = false;
        WarpStartTime = 0f;
        // Note: LastDestination is intentionally NOT cleared here
        // It should persist until the player moves away
    }

    /// <summary>
    ///     Clears the last destination tracking.
    ///     Call this when player has moved away from the warp destination.
    /// </summary>
    public void ClearLastDestination()
    {
        LastDestination = null;
    }

    /// <inheritdoc />
    public override readonly string ToString()
    {
        if (IsWarping && PendingWarp.HasValue)
        {
            return $"WarpState(Warping to {PendingWarp.Value.TargetMap})";
        }

        if (LastDestination.HasValue)
        {
            return $"WarpState(At destination {LastDestination.Value})";
        }

        return "WarpState(Idle)";
    }
}
