using Arch.Core;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Components.Tiles;

namespace PokeSharp.Core.Queries;

/// <summary>
///     Centralized cache of commonly used ECS queries for optimal performance.
///     All queries are created once and reused to eliminate per-frame allocations.
/// </summary>
/// <remarks>
///     <para>
///         This static class provides pre-built QueryDescription instances for common entity queries
///         throughout the PokeSharp ECS architecture. By caching queries centrally, we achieve:
///     </para>
///     <list type="bullet">
///         <item>Zero per-frame allocations for query descriptions</item>
///         <item>Consistent query patterns across all systems</item>
///         <item>Easier maintenance and optimization</item>
///         <item>Type-safe query composition</item>
///     </list>
///     <para>
///         <b>Usage Example:</b>
///     </para>
///     <code>
///         // Instead of:
///         var query = new QueryDescription().WithAll&lt;Position, GridMovement&gt;();
///         world.Query(in query, (ref Position pos, ref GridMovement mov) => { });
///
///         // Use centralized query:
///         world.Query(in Queries.Movement, (ref Position pos, ref GridMovement mov) => { });
///     </code>
/// </remarks>
public static class Queries
{
    // ============================================================================
    // MOVEMENT QUERIES
    // ============================================================================

    /// <summary>
    ///     Entities with position and grid movement capability.
    ///     Used by MovementSystem for basic movement processing.
    /// </summary>
    public static readonly QueryDescription Movement = new QueryDescription()
        .WithAll<Position, GridMovement>();

    /// <summary>
    ///     Entities with position, grid movement, and animation.
    ///     Used by MovementSystem for movement with animation updates.
    /// </summary>
    public static readonly QueryDescription MovementWithAnimation = new QueryDescription()
        .WithAll<Position, GridMovement, Animation>();

    /// <summary>
    ///     Entities with position, grid movement, but NO animation.
    ///     Optimized query for movement without animation overhead.
    /// </summary>
    public static readonly QueryDescription MovementWithoutAnimation = new QueryDescription()
        .WithAll<Position, GridMovement>()
        .WithNone<Animation>();

    /// <summary>
    ///     Player movement entities (position, movement, and player tag).
    ///     Used for player-specific movement logic.
    /// </summary>
    public static readonly QueryDescription PlayerMovement = new QueryDescription()
        .WithAll<Position, GridMovement, Player>();

    /// <summary>
    ///     Entities with pending movement requests.
    ///     Used by MovementSystem to process movement requests.
    /// </summary>
    public static readonly QueryDescription MovementRequests = new QueryDescription()
        .WithAll<Position, GridMovement, MovementRequest>();

    /// <summary>
    ///     Entities with only MovementRequest (for cleanup).
    ///     Used to remove processed movement requests.
    /// </summary>
    public static readonly QueryDescription MovementRequestsOnly = new QueryDescription()
        .WithAll<MovementRequest>();

    // ============================================================================
    // COLLISION QUERIES
    // ============================================================================

    /// <summary>
    ///     Entities with position and collision capability.
    ///     Used for collision detection and spatial queries.
    /// </summary>
    public static readonly QueryDescription Collidable = new QueryDescription()
        .WithAll<Position, Collision>();

    /// <summary>
    ///     Solid collision entities (with collision but not player).
    ///     Used for checking walkability and obstacle detection.
    /// </summary>
    public static readonly QueryDescription SolidCollision = new QueryDescription()
        .WithAll<Position, Collision>()
        .WithNone<Player>();

    /// <summary>
    ///     Ledge tiles with position and ledge component.
    ///     Used for Pokemon-style ledge jumping logic.
    /// </summary>
    public static readonly QueryDescription Ledges = new QueryDescription()
        .WithAll<TilePosition, TileLedge>();

    // ============================================================================
    // RENDERING QUERIES
    // ============================================================================

    /// <summary>
    ///     Entities with position and sprite (basic renderable).
    ///     Used by rendering system for sprite rendering.
    /// </summary>
    public static readonly QueryDescription Renderable = new QueryDescription()
        .WithAll<Position, Sprite>();

    /// <summary>
    ///     Entities with position, sprite, and animation.
    ///     Used for animated sprite rendering.
    /// </summary>
    public static readonly QueryDescription AnimatedSprites = new QueryDescription()
        .WithAll<Position, Sprite, Animation>();

    /// <summary>
    ///     Static sprites without animation.
    ///     Optimized for non-animated rendering.
    /// </summary>
    public static readonly QueryDescription StaticSprites = new QueryDescription()
        .WithAll<Position, Sprite>()
        .WithNone<Animation>();

    // ============================================================================
    // NPC QUERIES
    // ============================================================================

    /// <summary>
    ///     All NPC entities with position and NPC tag.
    ///     Used for NPC systems and AI processing.
    /// </summary>
    public static readonly QueryDescription Npcs = new QueryDescription()
        .WithAll<Position, Npc>();

    /// <summary>
    ///     NPCs with behavior AI components.
    ///     Used by behavior systems for NPC AI logic.
    /// </summary>
    public static readonly QueryDescription NpcsWithBehavior = new QueryDescription()
        .WithAll<Position, Npc, Behavior>();

    /// <summary>
    ///     NPCs with interaction capability.
    ///     Used for interaction systems and dialogue triggers.
    /// </summary>
    public static readonly QueryDescription InteractableNpcs = new QueryDescription()
        .WithAll<Position, Npc, Interaction>();

    /// <summary>
    ///     NPCs with movement routes (patrol paths).
    ///     Used by PathfindingSystem for NPC movement.
    /// </summary>
    public static readonly QueryDescription NpcsWithRoutes = new QueryDescription()
        .WithAll<Position, GridMovement, MovementRoute>();

    // ============================================================================
    // TILE QUERIES
    // ============================================================================

    /// <summary>
    ///     Static tile entities with tile position and sprite.
    ///     Used for static tile rendering (no animation).
    /// </summary>
    public static readonly QueryDescription StaticTiles = new QueryDescription()
        .WithAll<TilePosition, TileSprite>()
        .WithNone<AnimatedTile>();

    /// <summary>
    ///     Animated tile entities with animation data.
    ///     Used by TileAnimationSystem for frame updates.
    /// </summary>
    public static readonly QueryDescription AnimatedTiles = new QueryDescription()
        .WithAll<TilePosition, TileSprite, AnimatedTile>();

    /// <summary>
    ///     All tile entities (any tile with tile position).
    ///     Used by spatial hash for tile indexing.
    /// </summary>
    public static readonly QueryDescription AllTiles = new QueryDescription()
        .WithAll<TilePosition>();

    /// <summary>
    ///     Tiles with scripts (interactive tiles).
    ///     Used for tile interaction and scripting systems.
    /// </summary>
    public static readonly QueryDescription ScriptedTiles = new QueryDescription()
        .WithAll<TilePosition, TileScript>();

    // ============================================================================
    // SPATIAL QUERIES
    // ============================================================================

    /// <summary>
    ///     All entities with dynamic Position component.
    ///     Used by SpatialHashSystem for dynamic entity indexing.
    /// </summary>
    public static readonly QueryDescription AllPositioned = new QueryDescription()
        .WithAll<Position>();

    /// <summary>
    ///     All entities with static TilePosition component.
    ///     Used by SpatialHashSystem for static tile indexing.
    /// </summary>
    public static readonly QueryDescription AllTilePositioned = new QueryDescription()
        .WithAll<TilePosition>();

    // ============================================================================
    // MAP QUERIES
    // ============================================================================

    /// <summary>
    ///     Map metadata entities with MapInfo component.
    ///     Used for querying map dimensions, tile size, etc.
    /// </summary>
    public static readonly QueryDescription MapInfo = new QueryDescription()
        .WithAll<MapInfo>();

    /// <summary>
    ///     Encounter zone entities for wild Pokemon battles.
    ///     Used by encounter systems to trigger battles.
    /// </summary>
    public static readonly QueryDescription EncounterZones = new QueryDescription()
        .WithAll<EncounterZone, TilePosition>();

    // ============================================================================
    // PLAYER QUERIES
    // ============================================================================

    /// <summary>
    ///     Player entity with full component set.
    ///     Used for player-specific systems and input handling.
    /// </summary>
    public static readonly QueryDescription Player = new QueryDescription()
        .WithAll<Position, GridMovement, Player>();

    /// <summary>
    ///     Player with animation for player rendering.
    ///     Used by rendering systems for player sprite updates.
    /// </summary>
    public static readonly QueryDescription PlayerWithAnimation = new QueryDescription()
        .WithAll<Position, GridMovement, Player, Animation>();

    // ============================================================================
    // PATHFINDING QUERIES
    // ============================================================================

    /// <summary>
    ///     Entities with position, movement, and movement routes.
    ///     Used by PathfindingSystem for path following.
    /// </summary>
    public static readonly QueryDescription PathFollowers = new QueryDescription()
        .WithAll<Position, GridMovement, MovementRoute>();
}
