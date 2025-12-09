using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     Fluent builder for spawning and configuring NPCs.
///     Obtained via <see cref="IEntityApi.CreateNpc(int, int)"/>.
/// </summary>
/// <example>
///     <code>
/// // Spawn a fully configured NPC with fluent builder
/// var guard = Entity.CreateNpc(10, 15)
///     .FromDefinition(GameNpcId.Parse("base:npc:town/guard"))
///     .WithSprite(GameSpriteId.Parse("base:sprite:npc/guard"))
///     .WithBehavior(GameBehaviorId.Parse("base:behavior:patrol/simple"))
///     .WithDisplayName("Town Guard")
///     .Visible()
///     .Spawn();
///     </code>
/// </example>
public interface INpcSpawnBuilder
{
    /// <summary>
    ///     Use an NPC definition as the base template.
    /// </summary>
    INpcSpawnBuilder FromDefinition(GameNpcId npcId);

    /// <summary>
    ///     Set the sprite for the NPC.
    /// </summary>
    INpcSpawnBuilder WithSprite(GameSpriteId spriteId);

    /// <summary>
    ///     Set the behavior script for the NPC.
    /// </summary>
    INpcSpawnBuilder WithBehavior(GameBehaviorId behaviorId);

    /// <summary>
    ///     Set the display name for the NPC.
    /// </summary>
    INpcSpawnBuilder WithDisplayName(string displayName);

    /// <summary>
    ///     Set whether the NPC is visible (default: true).
    /// </summary>
    INpcSpawnBuilder Visible(bool visible = true);

    /// <summary>
    ///     Link NPC visibility to a game flag.
    ///     When hideWhenTrue is true (default), the NPC is hidden when the flag is set (FLAG_HIDE_* pattern).
    ///     When hideWhenTrue is false, the NPC is shown when the flag is set (FLAG_SHOW_* pattern).
    /// </summary>
    INpcSpawnBuilder WithVisibilityFlag(GameFlagId flagId, bool hideWhenTrue = true);

    /// <summary>
    ///     Hide the NPC when the specified flag is set (FLAG_HIDE_* pattern).
    ///     Shorthand for WithVisibilityFlag(flagId, hideWhenTrue: true).
    /// </summary>
    INpcSpawnBuilder HideWhenFlagSet(GameFlagId flagId);

    /// <summary>
    ///     Show the NPC when the specified flag is set (FLAG_SHOW_* pattern).
    ///     Shorthand for WithVisibilityFlag(flagId, hideWhenTrue: false).
    /// </summary>
    INpcSpawnBuilder ShowWhenFlagSet(GameFlagId flagId);

    /// <summary>
    ///     Set initial facing direction.
    /// </summary>
    INpcSpawnBuilder Facing(Ecs.Components.Movement.Direction direction);

    /// <summary>
    ///     Set a patrol path for the NPC.
    /// </summary>
    INpcSpawnBuilder WithPath(Microsoft.Xna.Framework.Point[] waypoints, bool loop = false);

    /// <summary>
    ///     Set the initial animation for the NPC.
    /// </summary>
    INpcSpawnBuilder WithAnimation(string animationName);

    /// <summary>
    ///     Set whether the NPC blocks movement.
    /// </summary>
    INpcSpawnBuilder WithCollision(bool isSolid = true);

    /// <summary>
    ///     Make the NPC solid (blocks movement). This is the default for NPCs.
    /// </summary>
    INpcSpawnBuilder Solid();

    /// <summary>
    ///     Make the NPC non-solid (can be walked through).
    /// </summary>
    INpcSpawnBuilder NonSolid();

    /// <summary>
    ///     Configure this NPC as a trainer who can battle the player.
    /// </summary>
    /// <param name="viewRange">How many tiles the trainer can see to spot the player (default 5).</param>
    INpcSpawnBuilder AsTrainer(int viewRange = 5);

    /// <summary>
    ///     Set how far the NPC can see (in tiles).
    ///     Used for trainer battles and NPC reactions.
    /// </summary>
    INpcSpawnBuilder WithViewRange(int tiles);

    /// <summary>
    ///     Mark the trainer as already defeated.
    ///     Useful for restoring game state.
    /// </summary>
    INpcSpawnBuilder AlreadyDefeated();

    /// <summary>
    ///     Set the movement range for wander behaviors.
    ///     The NPC will stay within this range from spawn position.
    /// </summary>
    INpcSpawnBuilder WithMovementRange(int rangeX, int rangeY);

    /// <summary>
    ///     Set the movement speed in tiles per second.
    ///     Default is 3.75 (MOVE_SPEED_NORMAL).
    /// </summary>
    INpcSpawnBuilder WithMovementSpeed(float tilesPerSecond);

    /// <summary>
    ///     Set the elevation level for rendering and collision.
    ///     Default is 3 (standard ground level).
    /// </summary>
    INpcSpawnBuilder AtElevation(byte elevation);

    /// <summary>
    ///     Enable player interaction with this NPC.
    /// </summary>
    /// <param name="range">Interaction range in tiles (default 1 = adjacent).</param>
    /// <param name="requiresFacing">Whether player must face the NPC to interact (default true).</param>
    INpcSpawnBuilder WithInteraction(int range = 1, bool requiresFacing = true);

    /// <summary>
    ///     Set a dialogue script to run when the NPC is interacted with.
    ///     Automatically enables interaction if not already enabled.
    /// </summary>
    /// <param name="scriptPath">Path to dialogue script (e.g., "dialogues/nurse_heal.csx").</param>
    INpcSpawnBuilder WithDialogue(string scriptPath);

    /// <summary>
    ///     Set an event to trigger when the NPC is interacted with.
    ///     Automatically enables interaction if not already enabled.
    /// </summary>
    /// <param name="eventName">Event name to trigger (e.g., "on_interact_pc").</param>
    INpcSpawnBuilder OnInteract(string eventName);

    /// <summary>
    ///     Enable or disable interaction capability.
    /// </summary>
    INpcSpawnBuilder Interactable(bool enabled = true);

    /// <summary>
    ///     Set the map context for the NPC position.
    ///     Used when spawning NPCs on a specific map with proper tile sizing.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileSize">Tile size in pixels (default 16).</param>
    INpcSpawnBuilder OnMap(Engine.Core.Types.GameMapId mapId, int tileSize = 16);

    /// <summary>
    ///     Set the parent entity for relationship tracking.
    ///     Used to associate NPCs with their owning map entity.
    /// </summary>
    /// <param name="parentEntity">The parent entity (typically MapInfoEntity).</param>
    INpcSpawnBuilder WithParent(Arch.Core.Entity parentEntity);

    /// <summary>
    ///     Spawn the configured NPC entity.
    ///     This is the terminal operation that creates the entity.
    /// </summary>
    /// <returns>The spawned NPC entity.</returns>
    Entity Spawn();

    /// <summary>
    ///     Spawn the NPC and return a fluent context for further operations.
    ///     Useful for chaining spawn with immediate manipulation.
    /// </summary>
    /// <example>
    ///     <code>
    /// Entity.CreateNpc(10, 15)
    ///     .WithSprite(spriteId)
    ///     .SpawnAndConfigure()
    ///     .Move(Direction.North)
    ///     .Face(Direction.South);
    ///     </code>
    /// </example>
    INpcContext SpawnAndConfigure();
}
