using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;

namespace PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Provides unified context for both entity-level and global scripts.
///     This is the primary interface for scripts to interact with the ECS world.
/// </summary>
/// <remarks>
///     <para>
///         ScriptContext serves as the bridge between scripts and the ECS architecture.
///         It provides type-safe component access, logging, entity management, and API services.
///     </para>
///     <example>
///         Entity script example:
///         <code>
/// // Create context (typically handled by ScriptService)
/// var apis = serviceProvider.GetRequiredService&lt;IScriptingApiProvider&gt;();
/// var ctx = new ScriptContext(world, entity, logger, apis);
///
/// public void Execute(ScriptContext ctx)
/// {
///     if (ctx.TryGetState&lt;Health&gt;(out var health))
///     {
///         ctx.Logger.LogInformation("Entity has {HP} HP", health.Current);
///     }
///
///     // Use API services (accessed via facade)
///     var playerMoney = ctx.Player.GetMoney();
///     ctx.Logger.LogInformation("Player has {Money} money", playerMoney);
/// }
/// </code>
///     </example>
///     <example>
///         Global script example:
///         <code>
/// public void Execute(ScriptContext ctx)
/// {
///     var query = ctx.World.Query(in new QueryDescription().WithAll&lt;Player&gt;());
///     foreach (var entity in query)
///     {
///         // Process all players
///     }
///
///     // Use domain APIs
///     ctx.Player.GiveMoney(100);
/// }
/// </code>
///     </example>
/// </remarks>
public sealed class ScriptContext
{
    private readonly IScriptingApiProvider _apis;
    private readonly Entity? _entity;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptContext" /> class.
    /// </summary>
    /// <param name="world">The ECS world instance.</param>
    /// <param name="entity">The target entity for entity-level scripts, or null for global scripts.</param>
    /// <param name="logger">Logger instance for this script's execution.</param>
    /// <param name="apis">The scripting API provider facade (provides access to all domain-specific APIs).</param>
    /// <remarks>
    ///     <para>
    ///         This constructor uses the facade pattern to reduce parameter count from 9 to 4.
    ///         The <paramref name="apis" /> provider supplies all domain-specific API services.
    ///     </para>
    ///     <para>
    ///         Typically, you won't construct this directly - ScriptService handles instantiation.
    ///     </para>
    /// </remarks>
    public ScriptContext(World world, Entity? entity, ILogger logger, IScriptingApiProvider apis)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entity = entity;
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
    }

    #region Core Properties

    /// <summary>
    ///     Gets the ECS world instance for direct world queries and operations.
    /// </summary>
    /// <remarks>
    ///     Use this for queries, bulk operations, or when you need direct world access.
    ///     For single-entity component access, prefer the type-safe helpers.
    /// </remarks>
    public World World { get; }

    /// <summary>
    ///     Gets the target entity for this script context, or null if this is a global script.
    /// </summary>
    /// <remarks>
    ///     Always check <see cref="IsEntityScript" /> or <see cref="IsGlobalScript" />
    ///     before accessing this property directly.
    /// </remarks>
    public Entity? Entity => _entity;

    /// <summary>
    ///     Gets the logger instance for this script's execution.
    /// </summary>
    /// <remarks>
    ///     Use this for debugging, error reporting, and tracking script execution.
    ///     Each script context has its own logger scope.
    /// </remarks>
    public ILogger Logger { get; }

    #endregion

    #region API Services

    /// <summary>
    ///     Gets the Player API service for player-related operations.
    /// </summary>
    /// <remarks>
    ///     Use this to interact with the player entity, manage money, position, and movement.
    ///     Accessed via the API provider facade.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var playerMoney = ctx.Player.GetMoney();
    /// ctx.Player.GiveMoney(100);
    /// var facing = ctx.Player.GetPlayerFacing();
    /// </code>
    /// </example>
    public PlayerApiService Player => _apis.Player;

    /// <summary>
    ///     Gets the NPC API service for NPC-related operations.
    /// </summary>
    /// <remarks>
    ///     Use this to control NPCs, move them, face directions, and manage paths.
    ///     Accessed via the API provider facade.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ctx.Npc.FaceEntity(npcEntity, playerEntity);
    /// ctx.Npc.MoveNPC(npcEntity, Direction.North);
    /// </code>
    /// </example>
    public NpcApiService Npc => _apis.Npc;

    /// <summary>
    ///     Gets the Map API service for map queries and transitions.
    /// </summary>
    /// <remarks>
    ///     Use this to check walkability, query entities at positions, and transition between maps.
    ///     Accessed via the API provider facade.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var isWalkable = ctx.Map.IsPositionWalkable(mapId, x, y);
    /// var entities = ctx.Map.GetEntitiesAt(mapId, x, y);
    /// ctx.Map.TransitionToMap(2, 10, 10);
    /// </code>
    /// </example>
    public MapApiService Map => _apis.Map;

    /// <summary>
    ///     Gets the Game State API service for managing flags and variables.
    /// </summary>
    /// <remarks>
    ///     Use this to manage game state through flags (booleans) and variables (strings).
    ///     Accessed via the API provider facade.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ctx.GameState.SetFlag("quest_completed", true);
    /// if (ctx.GameState.GetFlag("has_key"))
    /// {
    ///     ctx.GameState.SetVariable("door_state", "unlocked");
    /// }
    /// </code>
    /// </example>
    public GameStateApiService GameState => _apis.GameState;

    /// <summary>
    ///     Gets the Dialogue API service for displaying messages and text.
    /// </summary>
    /// <remarks>
    ///     Use this to show dialogue boxes, messages, and text to the player.
    ///     Accessed via the API provider facade.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ctx.Dialogue.ShowMessage("Hello, traveler!");
    /// ctx.Dialogue.ShowDialogue(npcEntity, "Welcome to my shop.");
    /// </code>
    /// </example>
    public DialogueApiService Dialogue => _apis.Dialogue;

    /// <summary>
    ///     Gets the Effects API service for spawning visual effects.
    /// </summary>
    /// <remarks>
    ///     Use this to create visual effects, animations, and particles in the game world.
    ///     Accessed via the API provider facade.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ctx.Effects.SpawnEffect("explosion", x, y);
    /// ctx.Effects.PlayAnimation(entity, "hit");
    /// </code>
    /// </example>
    public EffectApiService Effects => _apis.Effects;

    #endregion

    #region Context Type Properties

    /// <summary>
    ///     Gets a value indicating whether this context represents an entity-level script.
    /// </summary>
    /// <remarks>
    ///     When true, the <see cref="Entity" /> property contains a valid entity,
    ///     and component access methods like <see cref="GetState{T}" /> can be used.
    /// </remarks>
    public bool IsEntityScript => _entity.HasValue;

    /// <summary>
    ///     Gets a value indicating whether this context represents a global script.
    /// </summary>
    /// <remarks>
    ///     When true, the <see cref="Entity" /> property is null,
    ///     and you must use world queries to access entities.
    ///     Component access methods will throw exceptions.
    /// </remarks>
    public bool IsGlobalScript => !_entity.HasValue;

    #endregion

    #region Type-Safe Component Access

    /// <summary>
    ///     Gets a reference to the specified component on the target entity.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <returns>A reference to the component.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called on a global script context (no entity).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the entity doesn't have the specified component.
    /// </exception>
    /// <remarks>
    ///     This method throws on failure. Use <see cref="TryGetState{T}" /> for safe access.
    ///     Returns a reference for zero-allocation component modification.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ref var health = ref ctx.GetState&lt;Health&gt;();
    /// health.Current -= 10; // Modifies component directly
    /// </code>
    /// </example>
    public ref T GetState<T>()
        where T : struct
    {
        if (!_entity.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot get state of type '{typeof(T).Name}' for global script. "
                    + "Use TryGetState instead, or check IsEntityScript before calling."
            );
        }

        if (!World.Has<T>(_entity.Value))
        {
            throw new InvalidOperationException(
                $"Entity {_entity.Value.Id} does not have component '{typeof(T).Name}'. "
                    + "Use HasState or TryGetState to check existence first."
            );
        }

        return ref World.Get<T>(_entity.Value);
    }

    /// <summary>
    ///     Attempts to get the specified component from the target entity.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <param name="state">When this method returns, contains the component if found; otherwise, the default value.</param>
    /// <returns>true if the component exists; otherwise, false.</returns>
    /// <remarks>
    ///     This is the safe way to access components. Always prefer this over <see cref="GetState{T}" />
    ///     unless you're certain the component exists and you're in an entity script.
    ///     Returns false for global scripts or missing components without throwing.
    /// </remarks>
    /// <example>
    ///     <code>
    /// if (ctx.TryGetState&lt;Health&gt;(out var health))
    /// {
    ///     ctx.Logger.LogInformation("HP: {Current}/{Max}", health.Current, health.Max);
    /// }
    /// </code>
    /// </example>
    public bool TryGetState<T>(out T state)
        where T : struct
    {
        state = default;

        if (!_entity.HasValue)
        {
            return false;
        }

        if (!World.Has<T>(_entity.Value))
        {
            return false;
        }

        state = World.Get<T>(_entity.Value);
        return true;
    }

    /// <summary>
    ///     Gets the specified component if it exists, or adds it with default values if it doesn't.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve or add.</typeparam>
    /// <returns>A reference to the component (existing or newly added).</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called on a global script context (no entity).
    /// </exception>
    /// <remarks>
    ///     This is useful for lazy initialization of optional components.
    ///     The component is added with default struct values if it doesn't exist.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Ensure entity has a timer, create if needed
    /// ref var timer = ref ctx.GetOrAddState&lt;ScriptTimer&gt;();
    /// timer.ElapsedSeconds += deltaTime;
    /// </code>
    /// </example>
    public ref T GetOrAddState<T>()
        where T : struct
    {
        if (!_entity.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot get or add state of type '{typeof(T).Name}' for global script. "
                    + "Use TryGetState or check IsEntityScript before calling."
            );
        }

        Entity entity = _entity.Value;

        if (!World.Has<T>(entity))
        {
            World.Add(entity, default(T));
            Logger.LogDebug(
                "Added component {ComponentType} to entity {EntityId}",
                typeof(T).Name,
                entity.Id
            );
        }

        return ref World.Get<T>(entity);
    }

    /// <summary>
    ///     Checks whether the target entity has the specified component.
    /// </summary>
    /// <typeparam name="T">The component type to check.</typeparam>
    /// <returns>true if the entity has the component; false if it doesn't or this is a global script.</returns>
    /// <remarks>
    ///     Safe to call on both entity and global scripts. Returns false for global scripts.
    ///     Use this before calling <see cref="GetState{T}" /> if you're unsure whether the component exists.
    /// </remarks>
    /// <example>
    ///     <code>
    /// if (ctx.HasState&lt;Inventory&gt;())
    /// {
    ///     ref var inventory = ref ctx.GetState&lt;Inventory&gt;();
    ///     // Work with inventory
    /// }
    /// </code>
    /// </example>
    public bool HasState<T>()
        where T : struct
    {
        return _entity.HasValue && World.Has<T>(_entity.Value);
    }

    /// <summary>
    ///     Removes the specified component from the target entity.
    /// </summary>
    /// <typeparam name="T">The component type to remove.</typeparam>
    /// <returns>true if the component was removed; false if it didn't exist or this is a global script.</returns>
    /// <remarks>
    ///     Safe to call even if the component doesn't exist. Returns false for global scripts.
    ///     Use this to clean up temporary or conditional components.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Remove temporary status effect
    /// if (ctx.RemoveState&lt;PoisonEffect&gt;())
    /// {
    ///     ctx.Logger.LogInformation("Poison effect removed");
    /// }
    /// </code>
    /// </example>
    public bool RemoveState<T>()
        where T : struct
    {
        if (!_entity.HasValue || !World.Has<T>(_entity.Value))
        {
            return false;
        }

        World.Remove<T>(_entity.Value);
        Logger.LogDebug(
            "Removed component {ComponentType} from entity {EntityId}",
            typeof(T).Name,
            _entity.Value.Id
        );
        return true;
    }

    #endregion

    #region Convenience Properties

    /// <summary>
    ///     Gets a reference to the entity's Position component.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called on a global script or when the entity doesn't have a Position component.
    /// </exception>
    /// <remarks>
    ///     This is a convenience shortcut for <c>GetState&lt;Position&gt;()</c>.
    ///     Use <see cref="HasPosition" /> to check existence first if you're unsure.
    /// </remarks>
    /// <example>
    ///     <code>
    /// if (ctx.HasPosition)
    /// {
    ///     ref var pos = ref ctx.Position;
    ///     pos.X += 10;
    /// }
    /// </code>
    /// </example>
    public ref Position Position => ref GetState<Position>();

    /// <summary>
    ///     Gets a value indicating whether the target entity has a Position component.
    /// </summary>
    /// <remarks>
    ///     This is a convenience shortcut for <c>HasState&lt;Position&gt;()</c>.
    ///     Returns false for global scripts or entities without positions.
    /// </remarks>
    public bool HasPosition => HasState<Position>();

    #endregion
}
