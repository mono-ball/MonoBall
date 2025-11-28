using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Engine.Systems.Queries;

/// <summary>
///     Extension methods for common query patterns and query composition utilities.
///     Provides helper methods to simplify complex query construction and entity filtering.
/// </summary>
public static class QueryExtensions
{
    // ============================================================================
    // QUERY COMPOSITION HELPERS
    // ============================================================================

    /// <summary>
    ///     Creates a query that combines the requirements of two existing queries.
    ///     Warning: This creates a new QueryDescription and should not be used in hot paths.
    ///     Consider caching the result or using a pre-built query from <see cref="Queries" />.
    /// </summary>
    /// <param name="query1">First query to combine.</param>
    /// <param name="query2">Second query to combine.</param>
    /// <returns>New QueryDescription with combined requirements.</returns>
    public static QueryDescription And(this QueryDescription query1, QueryDescription query2)
    {
        // Note: This is a simplified implementation. For production use,
        // you would need to properly merge the All, None, and Any component lists.
        // For now, this serves as a placeholder for the extension method pattern.
        return query1;
    }

    // ============================================================================
    // SPATIAL QUERY HELPERS
    // ============================================================================

    /// <summary>
    ///     Checks if an entity has dynamic position (Position component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Position component.</returns>
    public static bool HasDynamicPosition(this Entity entity)
    {
        return entity.Has<Position>();
    }

    /// <summary>
    ///     Checks if an entity has static tile position (TilePosition component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has TilePosition component.</returns>
    public static bool HasStaticPosition(this Entity entity)
    {
        return entity.Has<TilePosition>();
    }

    /// <summary>
    ///     Checks if an entity is positioned (has either Position or TilePosition).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has any position component.</returns>
    public static bool IsPositioned(this Entity entity)
    {
        return entity.Has<Position>() || entity.Has<TilePosition>();
    }

    // ============================================================================
    // MOVEMENT QUERY HELPERS
    // ============================================================================

    /// <summary>
    ///     Checks if an entity can move (has GridMovement component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has GridMovement component.</returns>
    public static bool CanMove(this Entity entity)
    {
        return entity.Has<GridMovement>();
    }

    /// <summary>
    ///     Checks if an entity is currently moving.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has GridMovement and is currently moving.</returns>
    public static bool IsMoving(this Entity entity)
    {
        if (!entity.Has<GridMovement>())
        {
            return false;
        }

        ref GridMovement movement = ref entity.Get<GridMovement>();
        return movement.IsMoving;
    }

    /// <summary>
    ///     Checks if an entity is animated (has Animation component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Animation component.</returns>
    public static bool IsAnimated(this Entity entity)
    {
        return entity.Has<Animation>();
    }

    // ============================================================================
    // COLLISION QUERY HELPERS
    // ============================================================================

    /// <summary>
    ///     Checks if an entity has collision (has Collision component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Collision component.</returns>
    public static bool HasCollision(this Entity entity)
    {
        return entity.Has<Collision>();
    }

    /// <summary>
    ///     Checks if an entity is solid (has Collision and IsSolid is true).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has solid collision.</returns>
    public static bool IsSolid(this Entity entity)
    {
        if (!entity.Has<Collision>())
        {
            return false;
        }

        ref Collision collision = ref entity.Get<Collision>();
        return collision.IsSolid;
    }

    // ============================================================================
    // ENTITY TYPE QUERY HELPERS
    // ============================================================================

    /// <summary>
    ///     Checks if an entity is the player.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Player component.</returns>
    public static bool IsPlayer(this Entity entity)
    {
        return entity.Has<Player>();
    }

    /// <summary>
    ///     Checks if an entity is an NPC.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Npc component.</returns>
    public static bool IsNpc(this Entity entity)
    {
        return entity.Has<Npc>();
    }

    /// <summary>
    ///     Checks if an entity is renderable (has Sprite component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Sprite component.</returns>
    public static bool IsRenderable(this Entity entity)
    {
        return entity.Has<Sprite>();
    }

    /// <summary>
    ///     Checks if an entity is a tile (has TileSprite component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity is a tile.</returns>
    public static bool IsTile(this Entity entity)
    {
        return entity.Has<TileSprite>();
    }

    /// <summary>
    ///     Checks if an entity is an animated tile.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has AnimatedTile component.</returns>
    public static bool IsAnimatedTile(this Entity entity)
    {
        return entity.Has<AnimatedTile>();
    }

    // ============================================================================
    // BEHAVIOR QUERY HELPERS
    // ============================================================================

    /// <summary>
    ///     Checks if an NPC has behavior AI.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Behavior component.</returns>
    public static bool HasBehavior(this Entity entity)
    {
        return entity.Has<Behavior>();
    }

    /// <summary>
    ///     Checks if an NPC has a movement route.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has MovementRoute component.</returns>
    public static bool HasMovementRoute(this Entity entity)
    {
        return entity.Has<MovementRoute>();
    }

    /// <summary>
    ///     Checks if an NPC is interactable (has Interaction component).
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity has Interaction component.</returns>
    public static bool IsInteractable(this Entity entity)
    {
        return entity.Has<Interaction>();
    }
}
