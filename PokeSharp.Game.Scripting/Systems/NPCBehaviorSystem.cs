using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Configuration;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Runtime;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Scripting.Systems;

/// <summary>
///     System responsible for executing NPC behavior scripts using the ScriptContext pattern.
///     Queries entities with behavior data and executes their OnTick methods.
/// </summary>
/// <remarks>
///     CLEAN ARCHITECTURE:
///     This system uses the unified TypeScriptBase pattern with ScriptContext instances.
///     Scripts are cached as singletons in TypeRegistry and executed with per-tick
///     ScriptContext instances to prevent state corruption.
/// </remarks>
public class NPCBehaviorSystem : SystemBase, IUpdateSystem
{
    private readonly IScriptingApiProvider _apis;
    private readonly PerformanceConfiguration _config;
    private readonly ILogger<NPCBehaviorSystem> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
    private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;
    private int _lastBehaviorSummaryCount;
    private int _tickCounter;

    public NPCBehaviorSystem(
        ILogger<NPCBehaviorSystem> logger,
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis,
        PerformanceConfiguration? config = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _config = config ?? PerformanceConfiguration.Default;
    }

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

        // Debug: Log first tick to confirm system is running
        if (_tickCounter == 0)
        {
            _logger.LogInformation("NPCBehaviorSystem: First update tick");
        }

        // Use centralized query for NPCs with behavior
        world.Query(
            in EcsQueries.NpcsWithBehavior,
            (Entity entity, ref Npc npc, ref Behavior behavior) =>
            {
                // Debug: Log first entity found
                if (_tickCounter == 0)
                {
                    _logger.LogInformation(
                        "Found NPC with behavior: npcId={NpcId}, behaviorTypeId={BehaviorTypeId}, isActive={IsActive}",
                        npc.NpcId,
                        behavior.BehaviorTypeId,
                        behavior.IsActive
                    );
                }

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
                            $"NPC {npc.NpcId}",
                            "Behavior activation",
                            $"script not found ({behavior.BehaviorTypeId})"
                        );
                        // Deactivate behavior (with cleanup if needed)
                        DeactivateBehavior(null, ref behavior, null, npc.NpcId, "script not found");
                        return;
                    }

                    // Cast to TypeScriptBase
                    if (scriptObj is not TypeScriptBase script)
                    {
                        _logger.LogEntityOperationInvalid(
                            $"NPC {npc.NpcId}",
                            "Behavior activation",
                            $"script type mismatch ({scriptObj.GetType().Name})"
                        );
                        // Deactivate behavior (with cleanup if needed)
                        DeactivateBehavior(
                            null,
                            ref behavior,
                            null,
                            npc.NpcId,
                            "wrong script type"
                        );
                        return;
                    }

                    // Create ScriptContext for this entity (with cached logger and API services)
                    string loggerKey = $"{behavior.BehaviorTypeId}.{npc.NpcId}";
                    ILogger scriptLogger = GetOrCreateLogger(loggerKey);
                    var context = new ScriptContext(world, entity, scriptLogger, _apis);

                    // Initialize on first tick
                    if (!behavior.IsInitialized)
                    {
                        _logger.LogWorkflowStatus(
                            "Activating behavior",
                            ("npc", npc.NpcId),
                            ("behavior", behavior.BehaviorTypeId)
                        );

                        script.OnActivated(context);
                        behavior.IsInitialized = true;
                    }

                    // Execute tick
                    script.OnTick(context, deltaTime);
                    behaviorCount++;
                }
                catch (Exception ex)
                {
                    // Isolate errors - one NPC's script error shouldn't crash all behaviors
                    _logger.LogExceptionWithContext(
                        ex,
                        "Behavior script error for NPC {NpcId}",
                        npc.NpcId
                    );
                    errorCount++;

                    // Deactivate behavior with cleanup
                    object? scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
                    if (scriptObj is TypeScriptBase script)
                    {
                        string loggerKey = $"{behavior.BehaviorTypeId}.{npc.NpcId}";
                        ILogger scriptLogger = GetOrCreateLogger(loggerKey);
                        var context = new ScriptContext(world, entity, scriptLogger, _apis);
                        DeactivateBehavior(
                            script,
                            ref behavior,
                            context,
                            npc.NpcId,
                            $"error: {ex.Message}"
                        );
                    }
                    else
                    {
                        behavior.IsActive = false;
                        // Still clean up logger even if script is wrong type
                        RemoveLogger(behavior.BehaviorTypeId, npc.NpcId);
                    }
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
                "Behavior tick summary",
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
    public void SetBehaviorRegistry(TypeRegistry<BehaviorDefinition> registry)
    {
        _behaviorRegistry = registry;
        _logger.LogWorkflowStatus("Behavior registry linked", ("behaviors", registry.Count));
    }

    /// <summary>
    ///     Gets or creates a logger for a specific NPC behavior.
    ///     Implements size limit to prevent unbounded memory growth.
    /// </summary>
    /// <param name="key">Logger key (behavior type + NPC ID)</param>
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

                return _loggerFactory.CreateLogger($"Script.{k}");
            }
        );
    }

    /// <summary>
    ///     Removes a logger from the cache when a behavior is deactivated.
    /// </summary>
    /// <param name="behaviorTypeId">Behavior type ID</param>
    /// <param name="npcId">NPC ID</param>
    private void RemoveLogger(string behaviorTypeId, string npcId)
    {
        string key = $"{behaviorTypeId}.{npcId}";
        _scriptLoggerCache.TryRemove(key, out _);
    }

    /// <summary>
    ///     Safely deactivates a behavior by calling OnDeactivated and cleaning up state.
    ///     Also removes the logger from cache to prevent memory leaks.
    /// </summary>
    /// <param name="script">The behavior script instance (null if not available).</param>
    /// <param name="behavior">Reference to the behavior component.</param>
    /// <param name="context">Script context for cleanup (null if not available).</param>
    /// <param name="npcId">NPC ID for logger cleanup.</param>
    /// <param name="reason">Reason for deactivation (for logging).</param>
    private void DeactivateBehavior(
        TypeScriptBase? script,
        ref Behavior behavior,
        ScriptContext? context,
        string npcId,
        string reason
    )
    {
        if (behavior.IsInitialized && script != null && context != null)
        {
            try
            {
                script.OnDeactivated(context);
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
                    "Error during OnDeactivated for {TypeId} on NPC {NpcId}: {Message}",
                    behavior.BehaviorTypeId,
                    npcId,
                    ex.Message
                );
            }

            behavior.IsInitialized = false;
        }

        behavior.IsActive = false;

        // Clean up logger to prevent memory leak
        RemoveLogger(behavior.BehaviorTypeId, npcId);
    }
}
