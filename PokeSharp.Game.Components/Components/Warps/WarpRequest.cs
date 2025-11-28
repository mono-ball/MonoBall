using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components.Warps;

/// <summary>
///     Represents a pending warp request to be processed by WarpExecutionSystem.
///     Stored in WarpState.PendingWarp when a warp is triggered.
/// </summary>
/// <remarks>
///     This struct captures all the information needed to execute a warp transition.
///     Using MapIdentifier instead of raw strings provides type safety and validation.
/// </remarks>
public readonly struct WarpRequest
{
    /// <summary>
    ///     The target map identifier (type-safe).
    /// </summary>
    public MapIdentifier TargetMap { get; init; }

    /// <summary>
    ///     Target X tile coordinate on the destination map.
    /// </summary>
    public int TargetX { get; init; }

    /// <summary>
    ///     Target Y tile coordinate on the destination map.
    /// </summary>
    public int TargetY { get; init; }

    /// <summary>
    ///     Target elevation on the destination map.
    /// </summary>
    public byte TargetElevation { get; init; }

    /// <summary>
    ///     Creates a new WarpRequest with the specified destination.
    /// </summary>
    /// <param name="targetMap">Target map identifier.</param>
    /// <param name="targetX">Target X tile coordinate.</param>
    /// <param name="targetY">Target Y tile coordinate.</param>
    /// <param name="targetElevation">Target elevation (default: 3).</param>
    public WarpRequest(MapIdentifier targetMap, int targetX, int targetY, byte targetElevation = 3)
    {
        TargetMap = targetMap;
        TargetX = targetX;
        TargetY = targetY;
        TargetElevation = targetElevation;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"WarpRequest(→ {TargetMap.Value} @ {TargetX},{TargetY} elev:{TargetElevation})";
    }
}

