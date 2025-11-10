using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Configuration;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Core.Scripting.Runtime;

namespace PokeSharp.Core.Systems;

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
public class NPCBehaviorSystem : ParallelSystemBase, IUpdateSystem
{
    private readonly PerformanceConfiguration _config;
    private readonly ILogger<NPCBehaviorSystem> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IScriptingApiProvider _apis;
    private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
    private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;
    private int _tickCounter;
    private int _lastBehaviorSummaryCount;

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
    /// Gets the update priority. Lower values execute first.
    /// NPC behavior executes at priority 75, after spatial hash (25) and before movement (100).
    /// </summary>
    public int UpdatePriority => SystemPriority.NpcBehavior;

    /// <summary>
    ///     System priority for NPC behaviors - runs after spatial hash, before movement.
    /// </summary>
    public override int Priority => SystemPriority.NpcBehavior;

    /// <summary>
    /// Components this system reads to execute NPC behaviors.
    /// </summary>
    public override List<Type> GetReadComponents() => new()
    {
        typeof(Components.NPCs.Npc),
        typeof(Components.Movement.Position)
    };

    /// <summary>
    /// Components this system writes (behaviors modify state and may queue movement).
    /// </summary>
    public override List<Type> GetWriteComponents() => new()
    {
        typeof(Components.NPCs.Behavior),
        typeof(Components.Movement.MovementRequest)
    };

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
        return _scriptLoggerCache.GetOrAdd(key, k =>
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
        });
    }

    /// <summary>
    ///     Removes a logger from the cache when a behavior is deactivated.
    /// </summary>
    /// <param name="behaviorTypeId">Behavior type ID</param>
    /// <param name="npcId">NPC ID</param>
    private void RemoveLogger(string behaviorTypeId, string npcId)
    {
        var key = $"{behaviorTypeId}.{npcId}";
        _scriptLoggerCache.TryRemove(key, out _);
    }

    public override void Initialize(World world)
    {
        base.Initialize(world);
        _logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", _behaviorRegistry?.Count ?? 0));
    }

    public override void Update(World world, float deltaTime)
    {
        if (_behaviorRegistry == null)
        {
            _logger.LogSystemUnavailable("Behavior registry", "not set on NPCBehaviorSystem");
            return;
        }

        var behaviorCount = 0;
        var errorCount = 0;

        // Use centralized query for NPCs with behavior
        world.Query(
            in Queries.Queries.NpcsWithBehavior,
            (Entity entity, ref Npc npc, ref Behavior behavior) =>
            {
                try
                {
                    // Skip if behavior is not active
                    if (!behavior.IsActive)
                        return;

                    // Get script from registry
                    var scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
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
                        DeactivateBehavior(null, ref behavior, null, npc.NpcId, "wrong script type");
                        return;
                    }

                    // Create ScriptContext for this entity (with cached logger and API services)
                    var loggerKey = $"{behavior.BehaviorTypeId}.{npc.NpcId}";
                    var scriptLogger = GetOrCreateLogger(loggerKey);
                    var context = new ScriptContext(
                        world,
                        entity,
                        scriptLogger,
                        _apis
                    );

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
                    var scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
                    if (scriptObj is TypeScriptBase script)
                    {
                        var loggerKey = $"{behavior.BehaviorTypeId}.{npc.NpcId}";
                        var scriptLogger = GetOrCreateLogger(loggerKey);
                        var context = new ScriptContext(world, entity, scriptLogger, _apis);
                        DeactivateBehavior(script, ref behavior, context, npc.NpcId, $"error: {ex.Message}");
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

        var shouldLogSummary =
            errorCount > 0
            || (_tickCounter % 60 == 0 && behaviorCount > 0)
            || (behaviorCount > 0 && behaviorCount != _lastBehaviorSummaryCount);

        if (shouldLogSummary)
            _logger.LogWorkflowStatus(
                "Behavior tick summary",
                ("executed", behaviorCount),
                ("errors", errorCount)
            );

        if (behaviorCount > 0)
            _lastBehaviorSummaryCount = behaviorCount;
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
