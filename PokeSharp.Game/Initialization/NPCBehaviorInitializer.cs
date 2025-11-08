using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Game.Systems;
using PokeSharp.Scripting.Services;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Initializes NPC behavior system with script compilation and type registry.
/// </summary>
public class NPCBehaviorInitializer(
    ILogger<NPCBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    ScriptService scriptService,
    TypeRegistry<BehaviorDefinition> behaviorRegistry,
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    IWorldApi worldApi
)
{
    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Load all behavior definitions from JSON
            var loadedCount = behaviorRegistry.LoadAllAsync().Result;
            logger.LogInformation("Loaded {Count} behavior definitions", loadedCount);

            // Load and compile behavior scripts for each type
            foreach (var typeId in behaviorRegistry.GetAllTypeIds())
            {
                var definition = behaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    logger.LogInformation(
                        "Loading behavior script for {TypeId}: {Script}",
                        typeId,
                        scripted.BehaviorScript
                    );

                    var scriptInstance = scriptService
                        .LoadScriptAsync(scripted.BehaviorScript)
                        .Result;

                    if (scriptInstance != null)
                    {
                        // Initialize script with world
                        scriptService.InitializeScript(scriptInstance, world);

                        // Register script instance in the registry
                        behaviorRegistry.RegisterScript(typeId, scriptInstance);

                        logger.LogInformation(
                            "✓ Loaded and initialized behavior: {TypeId}",
                            typeId
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
            var npcBehaviorLogger = loggerFactory.CreateLogger<NPCBehaviorSystem>();
            var npcBehaviorSystem = new NPCBehaviorSystem(
                npcBehaviorLogger,
                loggerFactory,
                playerApi,
                npcApi,
                mapApi,
                gameStateApi,
                worldApi
            );
            npcBehaviorSystem.SetBehaviorRegistry(behaviorRegistry);
            systemManager.RegisterSystem(npcBehaviorSystem);

            logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }
}