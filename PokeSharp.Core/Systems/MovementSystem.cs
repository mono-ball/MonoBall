using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;

namespace PokeSharp.Core.Systems;

/// <summary>
/// System that handles grid-based movement with smooth interpolation.
/// Implements Pokemon-style tile-by-tile movement.
/// </summary>
public class MovementSystem : BaseSystem
{
    private const int TileSize = 16;

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Movement;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Query all entities with Position and GridMovement components
        var query = new QueryDescription().WithAll<Position, GridMovement>();

        world.Query(in query, (ref Position position, ref GridMovement movement) =>
        {
            if (movement.IsMoving)
            {
                // Update movement progress based on speed and delta time
                movement.MovementProgress += movement.MovementSpeed * deltaTime;

                if (movement.MovementProgress >= 1.0f)
                {
                    // Movement complete - snap to target position
                    movement.MovementProgress = 1.0f;
                    position.PixelX = movement.TargetPosition.X;
                    position.PixelY = movement.TargetPosition.Y;

                    // Update grid coordinates
                    position.X = (int)(movement.TargetPosition.X / TileSize);
                    position.Y = (int)(movement.TargetPosition.Y / TileSize);

                    movement.CompleteMovement();
                }
                else
                {
                    // Interpolate between start and target positions
                    position.PixelX = MathHelper.Lerp(
                        movement.StartPosition.X,
                        movement.TargetPosition.X,
                        movement.MovementProgress);

                    position.PixelY = MathHelper.Lerp(
                        movement.StartPosition.Y,
                        movement.TargetPosition.Y,
                        movement.MovementProgress);
                }
            }
            else
            {
                // Ensure pixel position matches grid position when not moving
                position.SyncPixelsToGrid();
            }
        });
    }
}
