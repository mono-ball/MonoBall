using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameSystems.Movement;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;
using MonoBallFramework.Game.Scripting.Systems;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.Initialization.Behaviors;

/// <summary>
///     Initializes tile behavior system with script compilation and type registry.
/// </summary>
public class TileBehaviorInitializer(
    ILogger<TileBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    TypeRegistry<TileBehaviorDefinition> behaviorRegistry,
    ScriptService scriptService,
    IScriptingApiProvider apiProvider,
    IEventBus eventBus,
    CollisionService? collisionService = null
)
{
    /// <summary>
    ///     Initializes the tile behavior system with TypeRegistry and ScriptService.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Load all behavior definitions from JSON
            int loadedCount = await behaviorRegistry.LoadAllAsync();
            logger.LogWorkflowStatus("Tile behavior definitions loaded", ("count", loadedCount));

            // Load and compile behavior scripts for each type
            foreach (string typeId in behaviorRegistry.GetAllTypeIds())
            {
                TileBehaviorDefinition? definition = behaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    logger.LogWorkflowStatus(
                        "Compiling tile behavior script",
                        ("behavior", typeId),
                        ("script", scripted.BehaviorScript)
                    );

                    object? scriptInstance = await scriptService.LoadScriptAsync(
                        scripted.BehaviorScript
                    );

                    if (scriptInstance != null)
                    {
                        // Initialize script with world
                        scriptService.InitializeScript(scriptInstance, world);

                        // Register script instance in the registry
                        behaviorRegistry.RegisterScript(typeId, scriptInstance);

                        logger.LogWorkflowStatus(
                            "Tile behavior ready",
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

            // Register TileBehaviorSystem with API services
            ILogger<TileBehaviorSystem> tileBehaviorLogger =
                loggerFactory.CreateLogger<TileBehaviorSystem>();
            var tileBehaviorSystem = new TileBehaviorSystem(
                tileBehaviorLogger,
                loggerFactory,
                apiProvider,
                eventBus
            );
            tileBehaviorSystem.SetBehaviorRegistry(behaviorRegistry);
            systemManager.RegisterUpdateSystem(tileBehaviorSystem);

            // Link TileBehaviorSystem to CollisionService and MovementSystem if available
            if (collisionService != null)
            {
                collisionService.SetTileBehaviorSystem(tileBehaviorSystem);
                logger.LogInformation("Linked TileBehaviorSystem to CollisionService");
            }

            // Link TileBehaviorSystem to MovementSystem
            MovementSystem? movementSystem = systemManager.GetSystem<MovementSystem>();
            if (movementSystem != null)
            {
                movementSystem.SetTileBehaviorSystem(tileBehaviorSystem);
                logger.LogInformation("Linked TileBehaviorSystem to MovementSystem");
            }

            logger.LogSystemInitialized("TileBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize tile behavior system");
        }
    }
}
