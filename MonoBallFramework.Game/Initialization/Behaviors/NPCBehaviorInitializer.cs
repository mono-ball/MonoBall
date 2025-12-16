using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;
using MonoBallFramework.Game.Scripting.Systems;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.Initialization.Behaviors;

/// <summary>
///     Initializes NPC behavior system with script compilation and type registry.
///     Also loads mod behaviors from EF Core and compiles their scripts.
/// </summary>
public class NPCBehaviorInitializer(
    ILogger<NPCBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    SystemManager systemManager,
    TypeRegistry<BehaviorDefinition> behaviorRegistry,
    ScriptService scriptService,
    IScriptingApiProvider apiProvider,
    IEventBus eventBus,
    GameDataContext? gameDataContext = null,
    MapLifecycleManager? mapLifecycleManager = null
)
{
    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    ///     Also loads mod behaviors from EF Core and compiles their scripts.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Load all behavior definitions from JSON (base game, filesystem)
            int loadedCount = await behaviorRegistry.LoadAllAsync();
            logger.LogWorkflowStatus("Behavior definitions loaded (filesystem)", ("count", loadedCount));

            // Load and compile behavior scripts for base game types
            foreach (string typeId in behaviorRegistry.GetAllTypeIds())
            {
                BehaviorDefinition? definition = behaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    await CompileAndRegisterScriptAsync(typeId, scripted.BehaviorScript);
                }
            }

            // Load mod behaviors from EF Core and compile their scripts
            int modScriptCount = await LoadModBehaviorsFromEfCoreAsync();
            if (modScriptCount > 0)
            {
                logger.LogWorkflowStatus("Mod NPC behavior scripts compiled", ("count", modScriptCount));
            }

            // Register NPCBehaviorSystem with API services
            ILogger<NPCBehaviorSystem> npcBehaviorLogger =
                loggerFactory.CreateLogger<NPCBehaviorSystem>();
            var npcBehaviorSystem = new NPCBehaviorSystem(
                npcBehaviorLogger,
                loggerFactory,
                apiProvider,
                scriptService,
                eventBus
            );
            npcBehaviorSystem.SetBehaviorRegistry(behaviorRegistry);
            systemManager.RegisterUpdateSystem(npcBehaviorSystem);

            // Wire up NPCBehaviorSystem to MapLifecycleManager for behavior cleanup during entity destruction
            if (mapLifecycleManager != null)
            {
                mapLifecycleManager.SetNPCBehaviorSystem(npcBehaviorSystem);
                logger.LogInformation("NPCBehaviorSystem wired to MapLifecycleManager for behavior cleanup");
            }
            else
            {
                logger.LogWarning(
                    "MapLifecycleManager not available - behavior cleanup on map unload may cause AccessViolationException"
                );
            }

            logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", loadedCount + modScriptCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }

    /// <summary>
    ///     Compiles and registers a behavior script.
    /// </summary>
    private async Task<bool> CompileAndRegisterScriptAsync(string typeId, string scriptPath)
    {
        logger.LogWorkflowStatus(
            "Compiling behavior script",
            ("behavior", typeId),
            ("script", scriptPath)
        );

        object? scriptInstance = await scriptService.LoadScriptAsync(scriptPath);

        if (scriptInstance != null)
        {
            // Register script template in the registry (used to create per-entity instances)
            // NOTE: Do NOT initialize the template - per-entity instances are initialized separately
            behaviorRegistry.RegisterScript(typeId, scriptInstance);

            logger.LogWorkflowStatus(
                "Behavior ready",
                ("behavior", typeId),
                ("script", scriptPath)
            );
            return true;
        }
        else
        {
            logger.LogError(
                "✗ Failed to compile script for {TypeId}: {Script}",
                typeId,
                scriptPath
            );
            return false;
        }
    }

    /// <summary>
    ///     Loads mod behaviors from EF Core that have scripts and compiles them.
    ///     This bridges the gap between EF Core storage and TypeRegistry script compilation.
    /// </summary>
    private async Task<int> LoadModBehaviorsFromEfCoreAsync()
    {
        if (gameDataContext == null)
        {
            logger.LogDebug("GameDataContext not available - skipping EF Core mod behavior loading");
            return 0;
        }

        int compiledCount = 0;

        try
        {
            // Query EF Core for mod behaviors with scripts that aren't already in the registry
            List<BehaviorEntity> modBehaviors = await gameDataContext.Behaviors
                .Where(b => b.SourceMod != null && b.BehaviorScript != null)
                .ToListAsync();

            foreach (BehaviorEntity modBehavior in modBehaviors)
            {
                string typeId = modBehavior.BehaviorId.Value;

                // Skip if already registered (filesystem took precedence)
                if (behaviorRegistry.HasScript(typeId))
                {
                    logger.LogDebug("Mod behavior {TypeId} already has script registered", typeId);
                    continue;
                }

                // Register the definition in TypeRegistry if not present
                if (!behaviorRegistry.Contains(typeId))
                {
                    // Create BehaviorDefinition from EF Core entity
                    var definition = new BehaviorDefinition
                    {
                        DefinitionId = typeId,
                        Name = modBehavior.Name,
                        Description = modBehavior.Description,
                        BehaviorScript = modBehavior.BehaviorScript,
                        DefaultSpeed = modBehavior.DefaultSpeed,
                        PauseAtWaypoint = modBehavior.PauseAtWaypoint,
                        AllowInteractionWhileMoving = modBehavior.AllowInteractionWhileMoving
                    };
                    behaviorRegistry.Register(definition);
                    logger.LogDebug("Registered mod behavior definition: {TypeId}", typeId);
                }

                // Compile and register the script
                if (!string.IsNullOrEmpty(modBehavior.BehaviorScript))
                {
                    bool success = await CompileAndRegisterScriptAsync(typeId, modBehavior.BehaviorScript);
                    if (success)
                    {
                        compiledCount++;
                        logger.LogInformation(
                            "Compiled mod NPC behavior script: {TypeId} from {Mod}",
                            typeId,
                            modBehavior.SourceMod
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading mod behaviors from EF Core");
        }

        return compiledCount;
    }
}
