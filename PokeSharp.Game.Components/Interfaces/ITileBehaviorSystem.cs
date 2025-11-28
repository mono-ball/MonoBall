using Arch.Core;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Components.Interfaces;

/// <summary>
///     Interface for tile behavior system to break circular dependencies.
///     Implemented by TileBehaviorSystem in PokeSharp.Game.Scripting.
/// </summary>
public interface ITileBehaviorSystem
{
    /// <summary>
    ///     Checks if movement is blocked by tile behaviors.
    /// </summary>
    bool IsMovementBlocked(
        World world,
        Entity tileEntity,
        Direction fromDirection,
        Direction toDirection
    );

    /// <summary>
    ///     Gets forced movement direction from tile behaviors.
    /// </summary>
    Direction GetForcedMovement(World world, Entity tileEntity, Direction currentDirection);

    /// <summary>
    ///     Gets jump direction from tile behaviors.
    /// </summary>
    Direction GetJumpDirection(World world, Entity tileEntity, Direction fromDirection);

    /// <summary>
    ///     Gets required movement mode from tile behaviors (surf, dive).
    /// </summary>
    string? GetRequiredMovementMode(World world, Entity tileEntity);

    /// <summary>
    ///     Checks if running is allowed on this tile.
    /// </summary>
    bool AllowsRunning(World world, Entity tileEntity);
}
