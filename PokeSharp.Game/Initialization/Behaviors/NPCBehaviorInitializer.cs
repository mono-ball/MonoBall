using Microsoft.Extensions.Logging;
using PokeSharp.Game.Engine.Common.Logging;
using PokeSharp.Game.Engine.Core.Events;
using PokeSharp.Game.Engine.Core.Types;
using PokeSharp.Game.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Scripting.Systems;

namespace PokeSharp.Game.Initialization.Behaviors;

/// <summary>
///     Initializes NPC behavior system with script compilation and type registry.
/// </summary>
public class NPCBehaviorInitializer(
    ILogger<NPCBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    SystemManager systemManager,
    TypeRegistry<BehaviorDefinition> behaviorRegistry,
    ScriptService scriptService,
    IScriptingApiProvider apiProvider,
    IEventBus eventBus
)
{
    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Load all behavior definitions from JSON
            int loadedCount = await behaviorRegistry.LoadAllAsync();
            logger.LogWorkflowStatus("Behavior definitions loaded", ("count", loadedCount));

            // Load and compile behavior scripts for each type
            foreach (string typeId in behaviorRegistry.GetAllTypeIds())
            {
                BehaviorDefinition? definition = behaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    logger.LogWorkflowStatus(
                        "Compiling behavior script",
                        ("behavior", typeId),
                        ("script", scripted.BehaviorScript)
                    );

                    object? scriptInstance = await scriptService.LoadScriptAsync(
                        scripted.BehaviorScript
                    );

                    if (scriptInstance != null)
                    {
                        // Register script template in the registry (used to create per-entity instances)
                        // NOTE: Do NOT initialize the template - per-entity instances are initialized separately
                        behaviorRegistry.RegisterScript(typeId, scriptInstance);

                        logger.LogWorkflowStatus(
                            "Behavior ready",
                            ("behavior", typeId),
                            ("script", scripted.BehaviorScript)
                        );
                    }
                    else
                    {
                        logger.LogError(
                            "✗ Failed to compile script for {TypeId}: {Script}",
                            typeId,
                            scripted.BehaviorScript
                        );
                    }
                }
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

            logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }
}
