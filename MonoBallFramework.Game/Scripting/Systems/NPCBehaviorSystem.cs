using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Engine.Common.Configuration;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.System;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Scripting.Services;
using EcsQueries = MonoBallFramework.Game.Engine.Systems.Queries.Queries;

namespace MonoBallFramework.Game.Scripting.Systems;

/// <summary>
///     System responsible for executing NPC behavior scripts using the ScriptContext pattern.
///     Creates per-entity script instances to support event-driven architecture.
/// </summary>
/// <remarks>
///     ARCHITECTURE:
///     Unlike tile behaviors which use singleton scripts with method parameters,
///     NPC behaviors use per-entity script instances with event subscriptions.
///     Each NPC gets its own script instance with its own Context and event handlers.
/// </remarks>
public class NPCBehaviorSystem : BehaviorSystemBase, IUpdateSystem
{
    private readonly ConcurrentDictionary<Entity, ScriptBase> _entityScriptCache = new();
    private readonly ILogger<NPCBehaviorSystem> _logger;
    private readonly ScriptService _scriptService;
    private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;

    public NPCBehaviorSystem(
        ILogger<NPCBehaviorSystem> logger,
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis,
        ScriptService scriptService,
        IEventBus? eventBus = null,
        PerformanceConfiguration? config = null
    ) : base(loggerFactory, apis, eventBus, config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
    }

    /// <inheritdoc />
    protected override string LoggerPrefix => "Script";

    /// <inheritdoc />
    protected override ILogger SystemLogger => _logger;

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     NPC behavior executes at priority 75, after spatial hash (25) and before movement (100).
    /// </summary>
    public int UpdatePriority => SystemPriority.NpcBehavior;

    /// <summary>
    ///     System priority for NPC behaviors - runs after spatial hash, before movement.
    /// </summary>
    public override int Priority => SystemPriority.NpcBehavior;

    public override void Initialize(World world)
    {
        base.Initialize(world);
        _logger.LogSystemInitialized(
            "NPCBehaviorSystem",
            ("behaviors", _behaviorRegistry?.Count ?? 0)
        );
    }

    public override void Update(World world, float deltaTime)
    {
        if (_behaviorRegistry == null)
        {
            _logger.LogSystemUnavailable("Behavior registry", "not set on NPCBehaviorSystem");
            return;
        }

        int behaviorCount = 0;
        int errorCount = 0;

        // Use centralized query for NPCs with behavior
        world.Query(
            in EcsQueries.NpcsWithBehavior,
            (Entity entity, ref Npc npc, ref Behavior behavior) =>
            {
                try
                {
                    // Skip if behavior is not active
                    if (!behavior.IsActive)
                    {
                        return;
                    }

                    // Get or create per-entity script instance
                    ScriptBase? script = GetOrCreateEntityScript(entity, behavior.BehaviorTypeId);
                    if (script == null)
                    {
                        _logger.LogEntityOperationInvalid(
                            $"NPC {npc.NpcId}",
                            "Behavior activation",
                            $"failed to create script instance ({behavior.BehaviorTypeId})"
                        );
                        DeactivateBehavior(
                            world,
                            entity,
                            null,
                            ref behavior,
                            null,
                            npc.NpcId,
                            "script creation failed"
                        );
                        return;
                    }

                    // Initialize script instance on first use
                    if (!behavior.IsInitialized)
                    {
                        string loggerKey = $"{behavior.BehaviorTypeId}.{npc.NpcId}";
                        ILogger scriptLogger = GetOrCreateLogger(loggerKey);

                        var context = new ScriptContext(
                            world,
                            entity,
                            scriptLogger,
                            Apis,
                            RequireEventBus()
                        );

                        _logger.LogWorkflowStatus(
                            "Activating behavior",
                            ("npc", npc.NpcId),
                            ("behavior", behavior.BehaviorTypeId)
                        );

                        script.Initialize(context);
                        script.RegisterEventHandlers(context);

                        behavior.IsInitialized = true;

                        // CRITICAL: Write component back to persist changes (structs passed by value in queries)
                        world.Set(entity, behavior);
                    }

                    behaviorCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogExceptionWithContext(
                        ex,
                        "Behavior script error for NPC {NpcId}",
                        npc.NpcId
                    );
                    errorCount++;

                    // Cleanup failed script
                    _entityScriptCache.TryRemove(entity, out _);
                    DeactivateBehavior(
                        world,
                        entity,
                        null,
                        ref behavior,
                        null,
                        npc.NpcId,
                        $"error: {ex.Message}"
                    );
                }
            }
        );

        // Publish TickEvent for all registered script event handlers to react
        if (behaviorCount > 0 && EventBus != null)
        {
            EventBus.Publish(new TickEvent { DeltaTime = deltaTime, TotalTime = 0f });
        }

        // Log performance metrics periodically using base class
        LogPerformanceMetrics(behaviorCount, errorCount, "Behavior tick summary");
    }

    /// <summary>
    ///     Gets or creates a per-entity script instance by cloning the singleton from the registry.
    /// </summary>
    private ScriptBase? GetOrCreateEntityScript(Entity entity, string behaviorTypeId)
    {
        // Check cache first
        if (_entityScriptCache.TryGetValue(entity, out ScriptBase? cachedScript))
        {
            return cachedScript;
        }

        // Get singleton template from registry
        object? templateObj = _behaviorRegistry?.GetScript(behaviorTypeId);
        if (templateObj is not ScriptBase template)
        {
            return null;
        }

        // Create new instance using Activator
        Type scriptType = template.GetType();
        object? newInstance = Activator.CreateInstance(scriptType);
        if (newInstance is not ScriptBase newScript)
        {
            _logger.LogError(
                "Failed to create instance of {ScriptType} for entity {EntityId}",
                scriptType.Name,
                entity.Id
            );
            return null;
        }

        // Cache and return
        _entityScriptCache[entity] = newScript;
        return newScript;
    }

    /// <summary>
    ///     Set the behavior registry for loading behavior scripts.
    /// </summary>
    public void SetBehaviorRegistry(TypeRegistry<BehaviorDefinition> registry)
    {
        _behaviorRegistry = registry;
        _logger.LogWorkflowStatus("Behavior registry linked", ("behaviors", registry.Count));
    }


    /// <summary>
    ///     Cleans up behavior script for an entity that is about to be destroyed.
    ///     Called by MapLifecycleManager before destroying NPC entities.
    ///     This ensures event subscriptions are properly disposed.
    /// </summary>
    /// <param name="entity">The entity being destroyed.</param>
    public void CleanupEntityBehavior(Entity entity)
    {
        // Extract NPC info BEFORE removing from cache (entity still has components)
        string? behaviorTypeId = null;
        string? npcId = null;

        if (World != null && World.IsAlive(entity))
        {
            if (World.Has<Behavior>(entity))
            {
                Behavior behavior = World.Get<Behavior>(entity);
                behaviorTypeId = behavior.BehaviorTypeId;
            }

            if (World.Has<Npc>(entity))
            {
                Npc npc = World.Get<Npc>(entity);
                npcId = npc.NpcId;
            }
        }

        if (!_entityScriptCache.TryRemove(entity, out ScriptBase? script))
        {
            return; // No script cached for this entity
        }

        try
        {
            // Call OnUnload to dispose event subscriptions
            script.OnUnload();

            // Cleanup logger to prevent memory leak
            if (behaviorTypeId != null && npcId != null)
            {
                RemoveLogger(behaviorTypeId, npcId);
            }

            _logger.LogDebug(
                "Cleaned up behavior script for entity {EntityId} before destruction",
                entity.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error during behavior cleanup for entity {EntityId}: {Message}",
                entity.Id,
                ex.Message
            );
        }
    }

    /// <summary>
    ///     Safely deactivates a behavior by calling OnUnload and cleaning up state.
    ///     Removes script instance from cache and logger to prevent memory leaks.
    /// </summary>
    /// <param name="world">ECS World for persisting component changes.</param>
    /// <param name="entity">Entity to update.</param>
    /// <param name="script">The behavior script instance (null if not available).</param>
    /// <param name="behavior">Reference to the behavior component.</param>
    /// <param name="context">Script context for cleanup (null if not available).</param>
    /// <param name="npcId">NPC ID for logger cleanup.</param>
    /// <param name="reason">Reason for deactivation (for logging).</param>
    private void DeactivateBehavior(
        World world,
        Entity entity,
        ScriptBase? script,
        ref Behavior behavior,
        ScriptContext? context,
        string npcId,
        string reason
    )
    {
        if (behavior.IsInitialized && script != null)
        {
            try
            {
                script.OnUnload();
                _logger.LogInformation(
                    "Deactivated behavior {TypeId} for NPC {NpcId}: {Reason}",
                    behavior.BehaviorTypeId,
                    npcId,
                    reason
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during OnUnload for {TypeId} on NPC {NpcId}: {Message}",
                    behavior.BehaviorTypeId,
                    npcId,
                    ex.Message
                );
            }

            behavior.IsInitialized = false;
        }

        behavior.IsActive = false;

        // CRITICAL: Write component back to persist changes (structs passed by value)
        world.Set(entity, behavior);

        // Clean up entity script instance
        _entityScriptCache.TryRemove(entity, out _);

        // Clean up logger to prevent memory leak
        RemoveLogger(behavior.BehaviorTypeId, npcId);
    }
}
