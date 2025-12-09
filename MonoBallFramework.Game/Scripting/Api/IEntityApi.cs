using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     Entity spawning and lifecycle management API for scripts.
///     Provides runtime entity creation from templates and definitions.
/// </summary>
/// <remarks>
///     <para>
///         This API enables scripts to dynamically spawn and manage entities at runtime.
///         All spawning methods integrate with the EntityFactoryService and respect
///         template inheritance, pooling, and validation.
///     </para>
///     <para>
///         Spawned entities are automatically added to the current map's entity collection
///         and will participate in all relevant systems (rendering, collision, etc.).
///     </para>
/// </remarks>
public interface IEntityApi
{
    #region Fluent NPC Builder

    /// <summary>
    ///     Creates a fluent builder for spawning a configurable NPC entity.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>A fluent builder for configuring and spawning the NPC.</returns>
    /// <example>
    ///     <code>
    /// // Spawn a fully configured NPC with fluent builder
    /// var guard = Entity.CreateNpc(10, 15)
    ///     .WithSprite(GameSpriteId.Parse("base:sprite:npc/guard"))
    ///     .WithBehavior(GameBehaviorId.Parse("base:behavior:patrol/simple"))
    ///     .WithDisplayName("Town Guard")
    ///     .Visible()
    ///     .Spawn();
    ///
    /// // Or spawn and immediately configure with chaining
    /// Entity.CreateNpc(5, 5)
    ///     .WithSprite(spriteId)
    ///     .SpawnAndConfigure()
    ///     .Move(Direction.North)
    ///     .Face(Direction.East);
    ///     </code>
    /// </example>
    INpcSpawnBuilder CreateNpc(int x, int y);

    #endregion

    #region NPC Spawning

    /// <summary>
    ///     Spawns an NPC from its definition at the specified position.
    /// </summary>
    /// <param name="npcId">The NPC definition ID.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>The spawned entity.</returns>
    /// <exception cref="ArgumentException">Thrown when NPC definition is not found.</exception>
    Entity SpawnNpc(GameNpcId npcId, int x, int y);

    /// <summary>
    ///     Spawns an NPC with a custom sprite override.
    /// </summary>
    /// <param name="npcId">The NPC definition ID.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="spriteId">Sprite to use instead of the definition's default.</param>
    /// <returns>The spawned entity.</returns>
    Entity SpawnNpc(GameNpcId npcId, int x, int y, GameSpriteId spriteId);

    /// <summary>
    ///     Spawns an NPC with a custom behavior override.
    /// </summary>
    /// <param name="npcId">The NPC definition ID.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="behaviorId">Behavior to use instead of the definition's default.</param>
    /// <returns>The spawned entity.</returns>
    Entity SpawnNpc(GameNpcId npcId, int x, int y, GameBehaviorId behaviorId);

    /// <summary>
    ///     Spawns an NPC with both sprite and behavior overrides.
    /// </summary>
    /// <param name="npcId">The NPC definition ID.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="spriteId">Sprite to use instead of the definition's default.</param>
    /// <param name="behaviorId">Behavior to use instead of the definition's default.</param>
    /// <returns>The spawned entity.</returns>
    Entity SpawnNpc(GameNpcId npcId, int x, int y, GameSpriteId spriteId, GameBehaviorId behaviorId);

    /// <summary>
    ///     Spawns a generic NPC at the specified position with explicit sprite and behavior.
    ///     Does not require an NPC definition - creates entity directly from components.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="spriteId">The sprite ID for the NPC's appearance.</param>
    /// <param name="behaviorId">Optional behavior ID. If null, NPC will be stationary.</param>
    /// <param name="displayName">Optional display name shown during interaction.</param>
    /// <returns>The spawned entity.</returns>
    Entity SpawnNpcAt(int x, int y, GameSpriteId spriteId, GameBehaviorId? behaviorId = null, string? displayName = null);

    #endregion

    #region Template Spawning

    /// <summary>
    ///     Spawns an entity from a template at the specified position.
    /// </summary>
    /// <param name="templateId">The entity template ID (e.g., "npc/generic", "trigger/warp").</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>The spawned entity.</returns>
    /// <exception cref="ArgumentException">Thrown when template is not found.</exception>
    Entity SpawnFromTemplate(string templateId, int x, int y);

    /// <summary>
    ///     Checks if a template exists in the template cache.
    /// </summary>
    /// <param name="templateId">The template ID to check.</param>
    /// <returns>True if template exists.</returns>
    bool TemplateExists(string templateId);

    #endregion

    #region Entity Lifecycle

    /// <summary>
    ///     Destroys an entity immediately.
    ///     The entity will be removed from all systems and its components will be cleaned up.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    void DestroyEntity(Entity entity);

    /// <summary>
    ///     Destroys an entity after a delay.
    ///     Useful for death animations or timed effects.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <param name="delaySeconds">Delay in seconds before destruction.</param>
    void DestroyEntityDelayed(Entity entity, float delaySeconds);

    /// <summary>
    ///     Checks if an entity exists and is alive.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if the entity is alive.</returns>
    bool IsAlive(Entity entity);

    #endregion

    #region Entity Queries

    /// <summary>
    ///     Finds an entity by its Arch entity ID.
    /// </summary>
    /// <param name="entityId">The Arch entity ID.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    Entity? FindEntityById(int entityId);

    /// <summary>
    ///     Finds all entities with the specified tag.
    /// </summary>
    /// <param name="tag">The tag to search for (e.g., "npc", "trainer", "item").</param>
    /// <returns>Array of matching entities.</returns>
    Entity[] FindEntitiesByTag(string tag);

    /// <summary>
    ///     Finds all NPCs within a radius of a position.
    /// </summary>
    /// <param name="centerX">Center tile X coordinate.</param>
    /// <param name="centerY">Center tile Y coordinate.</param>
    /// <param name="radius">Search radius in tiles.</param>
    /// <returns>Array of NPC entities within the radius.</returns>
    Entity[] FindNpcsInRadius(int centerX, int centerY, int radius);

    /// <summary>
    ///     Finds all entities at a specific tile position.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>Array of entities at that position.</returns>
    Entity[] FindEntitiesAt(int x, int y);

    /// <summary>
    ///     Gets the NPC ID for an entity if it has one.
    /// </summary>
    /// <param name="entity">The entity to query.</param>
    /// <returns>The NPC ID, or null if entity is not an NPC or has no ID.</returns>
    GameNpcId? GetNpcId(Entity entity);

    #endregion
}
