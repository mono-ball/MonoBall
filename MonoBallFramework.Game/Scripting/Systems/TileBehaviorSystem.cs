using System.Collections.Concurrent;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Common.Configuration;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Tile;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Components.Interfaces;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;

namespace MonoBallFramework.Game.Scripting.Systems;

/// <summary>
///     System responsible for executing tile behavior scripts using the ScriptContext pattern.
///     Handles collision checking, forced movement, and tile interactions.
/// </summary>
/// <remarks>
///     CLEAN ARCHITECTURE:
///     This system uses the unified TypeScriptBase pattern with ScriptContext instances.
///     Scripts are cached as singletons in TypeRegistry and executed with per-tick
///     ScriptContext instances to prevent state corruption.
/// </remarks>
public class TileBehaviorSystem : SystemBase, IUpdateSystem, ITileBehaviorSystem
{
    private readonly IScriptingApiProvider _apis;
    private readonly PerformanceConfiguration _config;
    private readonly IEventBus? _eventBus;
    private readonly ILogger<TileBehaviorSystem> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
    private TypeRegistry<TileBehaviorDefinition>? _behaviorRegistry;
    private int _lastBehaviorSummaryCount;
    private int _tickCounter;

    public TileBehaviorSystem(
        ILogger<TileBehaviorSystem> logger,
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis,
        IEventBus? eventBus = null,
        PerformanceConfiguration? config = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _eventBus = eventBus;
        _config = config ?? PerformanceConfiguration.Default;
    }

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Tile behavior executes at priority 50, after spatial hash (25) and before movement (100).
    /// </summary>
    public int UpdatePriority => SystemPriority.TileBehavior;

    /// <summary>
    ///     Checks if movement is blocked by tile behaviors.
    ///     Called by CollisionService.
    /// </summary>
    public bool IsMovementBlocked(
        World world,
        Entity tileEntity,
        Direction fromDirection,
        Direction toDirection
    )
    {
        if (!tileEntity.Has<TileBehavior>())
        {
            return false;
        }

        ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
        {
            return false;
        }

        if (_behaviorRegistry == null)
        {
            return false;
        }

        object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
        if (scriptObj is not TileBehaviorScriptBase script)
        {
            return false;
        }

        var context = new ScriptContext(
            world,
            tileEntity,
            _logger,
            _apis,
            _eventBus
                ?? throw new InvalidOperationException("EventBus is required for ScriptContext")
        );

        // Check both directions (like Pokemon Emerald's two-way check)
        if (script.IsBlockedFrom(context, fromDirection, toDirection))
        {
            return true;
        }

        if (script.IsBlockedTo(context, toDirection))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets forced movement direction from tile behaviors.
    ///     Called by MovementSystem.
    /// </summary>
    public Direction GetForcedMovement(World world, Entity tileEntity, Direction currentDirection)
    {
        if (!tileEntity.Has<TileBehavior>())
        {
            return Direction.None;
        }

        ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
        {
            return Direction.None;
        }

        if (_behaviorRegistry == null)
        {
            return Direction.None;
        }

        object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
        if (scriptObj is not TileBehaviorScriptBase script)
        {
            return Direction.None;
        }

        var context = new ScriptContext(
            world,
            tileEntity,
            _logger,
            _apis,
            _eventBus
                ?? throw new InvalidOperationException("EventBus is required for ScriptContext")
        );
        return script.GetForcedMovement(context, currentDirection);
    }

    /// <summary>
    ///     Gets jump direction from tile behaviors.
    ///     Called by MovementSystem.
    /// </summary>
    public Direction GetJumpDirection(World world, Entity tileEntity, Direction fromDirection)
    {
        if (!tileEntity.Has<TileBehavior>())
        {
            return Direction.None;
        }

        ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
        {
            return Direction.None;
        }

        if (_behaviorRegistry == null)
        {
            return Direction.None;
        }

        object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
        if (scriptObj is not TileBehaviorScriptBase script)
        {
            return Direction.None;
        }

        var context = new ScriptContext(
            world,
            tileEntity,
            _logger,
            _apis,
            _eventBus
                ?? throw new InvalidOperationException("EventBus is required for ScriptContext")
        );
        return script.GetJumpDirection(context, fromDirection);
    }

    /// <summary>
    ///     Gets required movement mode from tile behaviors (surf, dive).
    ///     Called by MovementSystem.
    /// </summary>
    public string? GetRequiredMovementMode(World world, Entity tileEntity)
    {
        if (!tileEntity.Has<TileBehavior>())
        {
            return null;
        }

        ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
        {
            return null;
        }

        if (_behaviorRegistry == null)
        {
            return null;
        }

        object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
        if (scriptObj is not TileBehaviorScriptBase script)
        {
            return null;
        }

        var context = new ScriptContext(
            world,
            tileEntity,
            _logger,
            _apis,
            _eventBus
                ?? throw new InvalidOperationException("EventBus is required for ScriptContext")
        );
        return script.GetRequiredMovementMode(context);
    }

    /// <summary>
    ///     Checks if running is allowed on this tile.
    ///     Called by MovementSystem.
    /// </summary>
    public bool AllowsRunning(World world, Entity tileEntity)
    {
        if (!tileEntity.Has<TileBehavior>())
        {
            return true; // Default: allow running
        }

        ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
        {
            return true;
        }

        if (_behaviorRegistry == null)
        {
            return true;
        }

        object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
        if (scriptObj is not TileBehaviorScriptBase script)
        {
            return true;
        }

        var context = new ScriptContext(
            world,
            tileEntity,
            _logger,
            _apis,
            _eventBus
                ?? throw new InvalidOperationException("EventBus is required for ScriptContext")
        );
        return script.AllowsRunning(context);
    }

    /// <summary>
    ///     System priority for tile behaviors - runs after spatial hash, before movement.
    /// </summary>
    public override int Priority => SystemPriority.TileBehavior;

    public override void Initialize(World world)
    {
        base.Initialize(world);
        _logger.LogSystemInitialized(
            "TileBehaviorSystem",
            ("behaviors", _behaviorRegistry?.Count ?? 0)
        );
    }

    public override void Update(World world, float deltaTime)
    {
        if (_behaviorRegistry == null)
        {
            _logger.LogSystemUnavailable("Behavior registry", "not set on TileBehaviorSystem");
            return;
        }

        int behaviorCount = 0;
        int errorCount = 0;

        // Query all tiles with behavior components
        world.Query(
            new QueryDescription().WithAll<TilePosition, TileBehavior>(),
            (Entity entity, ref TilePosition tilePos, ref TileBehavior behavior) =>
            {
                try
                {
                    // Skip if behavior is not active
                    if (!behavior.IsActive)
                    {
                        return;
                    }

                    // Get script from registry
                    object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
                    if (scriptObj == null)
                    {
                        _logger.LogEntityOperationInvalid(
                            $"Tile at ({tilePos.X}, {tilePos.Y})",
                            "Behavior activation",
                            $"script not found ({behavior.BehaviorTypeId})"
                        );
                        behavior.IsActive = false;
                        return;
                    }

                    // Cast to TileBehaviorScriptBase
                    if (scriptObj is not TileBehaviorScriptBase script)
                    {
                        _logger.LogEntityOperationInvalid(
                            $"Tile at ({tilePos.X}, {tilePos.Y})",
                            "Behavior activation",
                            $"script type mismatch ({scriptObj.GetType().Name})"
                        );
                        behavior.IsActive = false;
                        return;
                    }

                    // Create ScriptContext for this entity
                    string loggerKey = $"{behavior.BehaviorTypeId}.{entity.Id}";
                    ILogger scriptLogger = GetOrCreateLogger(loggerKey);
                    var context = new ScriptContext(
                        world,
                        entity,
                        scriptLogger,
                        _apis,
                        _eventBus
                            ?? throw new InvalidOperationException(
                                "EventBus is required for ScriptContext"
                            )
                    );

                    // Initialize on first tick
                    if (!behavior.IsInitialized)
                    {
                        script.OnActivated(context);
                        behavior.IsInitialized = true;
                    }

                    // Execute tick
                    script.OnTick(context, deltaTime);
                    behaviorCount++;
                }
                catch (Exception ex)
                {
                    // Isolate errors - one tile's script error shouldn't crash all behaviors
                    _logger.LogExceptionWithContext(
                        ex,
                        "Tile behavior script error at ({X}, {Y})",
                        tilePos.X,
                        tilePos.Y
                    );
                    errorCount++;
                    behavior.IsActive = false;
                }
            }
        );

        // Log performance metrics periodically
        _tickCounter++;

        bool shouldLogSummary =
            errorCount > 0
            || (_tickCounter % 60 == 0 && behaviorCount > 0)
            || (behaviorCount > 0 && behaviorCount != _lastBehaviorSummaryCount);

        if (shouldLogSummary)
        {
            _logger.LogWorkflowStatus(
                "Tile behavior tick summary",
                ("executed", behaviorCount),
                ("errors", errorCount)
            );
        }

        if (behaviorCount > 0)
        {
            _lastBehaviorSummaryCount = behaviorCount;
        }
    }

    /// <summary>
    ///     Set the behavior registry for loading behavior scripts.
    /// </summary>
    public void SetBehaviorRegistry(TypeRegistry<TileBehaviorDefinition> registry)
    {
        _behaviorRegistry = registry;
        _logger.LogWorkflowStatus("Tile behavior registry linked", ("behaviors", registry.Count));
    }

    /// <summary>
    ///     Called when an entity steps onto a tile.
    ///     Publishes TileSteppedOnEvent and calls OnStep on tile behavior scripts.
    ///     Returns false if the event was cancelled (entity should not step on tile).
    /// </summary>
    /// <param name="world">ECS world</param>
    /// <param name="entity">Entity stepping on tile</param>
    /// <param name="tileEntity">Tile entity being stepped on</param>
    /// <param name="tilePosition">Position of the tile</param>
    /// <returns>True if stepping is allowed, false if cancelled by script</returns>
    public bool OnEntityStepOnTile(
        World world,
        Entity entity,
        Entity tileEntity,
        TilePosition tilePosition
    )
    {
        // Get tile type from TileBehavior component if available
        string tileType = "unknown";
        if (tileEntity.Has<TileBehavior>())
        {
            ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
            tileType = behavior.BehaviorTypeId;
        }

        // Publish TileSteppedOnEvent if EventBus is available
        if (_eventBus != null)
        {
            var steppedOnEvent = new TileSteppedOnEvent
            {
                Entity = entity,
                TileX = tilePosition.X,
                TileY = tilePosition.Y,
                TileType = tileType,
                FromDirection = 0, // TODO: Get actual direction if needed
                Elevation = 0, // TODO: Get actual elevation if needed
            };

            _eventBus.Publish(steppedOnEvent);

            // If event was cancelled by a subscriber, block the step
            if (steppedOnEvent.IsCancelled)
            {
                _logger.LogDebug(
                    "Entity {EntityId} blocked from stepping on tile at ({X}, {Y}): {Reason}",
                    entity.Id,
                    tilePosition.X,
                    tilePosition.Y,
                    steppedOnEvent.CancellationReason ?? "No reason provided"
                );
                return false;
            }
        }

        // Call OnStep on tile behavior script (maintains backward compatibility)
        if (_behaviorRegistry != null && tileEntity.Has<TileBehavior>())
        {
            ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
            if (behavior.IsActive)
            {
                object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
                if (scriptObj is TileBehaviorScriptBase script)
                {
                    try
                    {
                        var context = new ScriptContext(
                            world,
                            tileEntity,
                            _logger,
                            _apis,
                            _eventBus
                                ?? throw new InvalidOperationException(
                                    "EventBus is required for ScriptContext"
                                )
                        );
                        script.OnStep(context, entity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogExceptionWithContext(
                            ex,
                            "Error in OnStep for tile at ({X}, {Y})",
                            tilePosition.X,
                            tilePosition.Y
                        );
                    }
                }
            }
        }

        return true; // Stepping allowed
    }

    /// <summary>
    ///     Called when an entity steps off a tile.
    ///     Publishes TileSteppedOffEvent and allows scripts to react to departure.
    /// </summary>
    /// <param name="world">ECS world</param>
    /// <param name="entity">Entity stepping off tile</param>
    /// <param name="tileEntity">Tile entity being stepped off</param>
    /// <param name="tilePosition">Position of the tile</param>
    public void OnEntityStepOffTile(
        World world,
        Entity entity,
        Entity tileEntity,
        TilePosition tilePosition
    )
    {
        // Get tile type from TileBehavior component if available
        string tileType = "unknown";
        if (tileEntity.Has<TileBehavior>())
        {
            ref TileBehavior behavior = ref tileEntity.Get<TileBehavior>();
            tileType = behavior.BehaviorTypeId;
        }

        // Publish TileSteppedOffEvent if EventBus is available
        if (_eventBus != null)
        {
            var steppedOffEvent = new TileSteppedOffEvent
            {
                Entity = entity,
                TileX = tilePosition.X,
                TileY = tilePosition.Y,
                TileType = tileType,
                NewTileX = tilePosition.X, // TODO: Get actual new tile position from movement system
                NewTileY = tilePosition.Y, // TODO: Get actual new tile position from movement system
                ExitDirection = 0, // TODO: Get actual exit direction if needed
                Elevation = 0, // TODO: Get actual elevation if needed
            };

            _eventBus.Publish(steppedOffEvent);
        }

        // Note: TileBehaviorScriptBase doesn't have an OnStepOff method yet
        // This is just for event publishing to support event-driven mods
    }

    /// <summary>
    ///     Gets or creates a logger for a specific tile behavior.
    ///     Implements size limit to prevent unbounded memory growth.
    /// </summary>
    /// <param name="key">Logger key (behavior type + entity ID)</param>
    /// <returns>Cached or newly created logger</returns>
    private ILogger GetOrCreateLogger(string key)
    {
        return _scriptLoggerCache.GetOrAdd(
            key,
            k =>
            {
                // Check if we've hit the cache limit
                if (_scriptLoggerCache.Count >= _config.MaxCachedLoggers)
                {
                    _logger.LogWarning(
                        "Script logger cache limit reached ({Limit}). Consider increasing limit or checking for leaks.",
                        _config.MaxCachedLoggers
                    );
                }

                return _loggerFactory.CreateLogger($"TileBehavior.{k}");
            }
        );
    }
}
