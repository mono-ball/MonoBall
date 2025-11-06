using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Scripting;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System responsible for executing NPC behavior scripts using the ScriptContext pattern.
///     Queries entities with BehaviorComponent and executes their OnTick methods.
/// </summary>
/// <remarks>
///     CLEAN ARCHITECTURE:
///     This system uses the unified TypeScriptBase pattern with ScriptContext instances.
///     Scripts are cached as singletons in TypeRegistry and executed with per-tick
///     ScriptContext instances to prevent state corruption.
/// </remarks>
public class NpcBehaviorSystem : BaseSystem
{
    private readonly ILogger<NpcBehaviorSystem> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;

    public NpcBehaviorSystem(ILogger<NpcBehaviorSystem> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    ///     System priority for NPC behaviors - runs after spatial hash, before movement.
    /// </summary>
    public override int Priority => SystemPriority.NpcBehavior;

    /// <summary>
    ///     Set the behavior registry for loading behavior scripts.
    /// </summary>
    public void SetBehaviorRegistry(TypeRegistry<BehaviorDefinition> registry)
    {
        _behaviorRegistry = registry;
        _logger.LogInformation("Behavior registry set with {Count} behaviors", registry.Count);
    }

    public override void Initialize(World world)
    {
        base.Initialize(world);
        _logger.LogInformation("NpcBehaviorSystem initialized");
    }

    public override void Update(World world, float deltaTime)
    {
        if (_behaviorRegistry == null)
        {
            _logger.LogWarning("Behavior registry not set on NpcBehaviorSystem");
            return;
        }

        // Query all NPCs with behavior components
        var query = new QueryDescription().WithAll<NpcComponent, BehaviorComponent, Position>();

        var behaviorCount = 0;
        var errorCount = 0;

        world.Query(
            in query,
            (Entity entity, ref NpcComponent npc, ref BehaviorComponent behavior) =>
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
                        _logger.LogWarning(
                            "Behavior script not found: {TypeId} (NPC: {NpcId})",
                            behavior.BehaviorTypeId,
                            npc.NpcId
                        );
                        behavior.IsActive = false;
                        return;
                    }

                    // Cast to TypeScriptBase
                    if (scriptObj is not TypeScriptBase script)
                    {
                        _logger.LogError(
                            "Script is not TypeScriptBase: {TypeId} (Type: {ActualType})",
                            behavior.BehaviorTypeId,
                            scriptObj.GetType().Name
                        );
                        behavior.IsActive = false;
                        return;
                    }

                    // Create ScriptContext for this entity
                    var scriptLogger = _loggerFactory.CreateLogger(
                        $"Script.{behavior.BehaviorTypeId}.{npc.NpcId}"
                    );
                    var context = new ScriptContext(world, entity, scriptLogger);

                    // Initialize on first tick
                    if (!behavior.IsInitialized)
                    {
                        _logger.LogInformation(
                            "Activating behavior script for NPC {NpcId}, type {TypeId}",
                            npc.NpcId,
                            behavior.BehaviorTypeId
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
                    _logger.LogError(
                        ex,
                        "Script error for NPC {NpcId}: {Message}",
                        npc.NpcId,
                        ex.Message
                    );
                    errorCount++;

                    // Disable the behavior to prevent repeated errors
                    behavior.IsActive = false;
                }
            }
        );

        // Log performance metrics periodically
        if (behaviorCount > 0)
            _logger.LogTrace(
                "Executed {Count} NPC behaviors ({Errors} errors)",
                behaviorCount,
                errorCount
            );
    }
}
