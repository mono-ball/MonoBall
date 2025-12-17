using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameSystems.Movement;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;
using MonoBallFramework.Game.Scripting.Systems;

namespace MonoBallFramework.Game.Initialization.Behaviors;

/// <summary>
///     Initializes tile behavior system with script compilation and type registry.
///     Also loads mod behaviors from EF Core and compiles their scripts.
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
    GameDataContext? gameDataContext = null,
    CollisionService? collisionService = null
)
{
    /// <summary>
    ///     Initializes the tile behavior system with TypeRegistry and ScriptService.
    ///     Also loads mod behaviors from EF Core and compiles their scripts.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Load all behavior definitions from JSON (base game, filesystem)
            int loadedCount = await behaviorRegistry.LoadAllAsync();
            logger.LogWorkflowStatus("Tile behavior definitions loaded (filesystem)", ("count", loadedCount));

            // Load and compile behavior scripts for base game types
            foreach (string typeId in behaviorRegistry.GetAllTypeIds())
            {
                TileBehaviorDefinition? definition = behaviorRegistry.Get(typeId);
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
                logger.LogWorkflowStatus("Mod tile behavior scripts compiled", ("count", modScriptCount));
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

            logger.LogSystemInitialized("TileBehaviorSystem", ("behaviors", loadedCount + modScriptCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize tile behavior system");
        }
    }

    /// <summary>
    ///     Compiles and registers a behavior script.
    /// </summary>
    private async Task<bool> CompileAndRegisterScriptAsync(string typeId, string scriptPath)
    {
        logger.LogWorkflowStatus(
            "Compiling tile behavior script",
            ("behavior", typeId),
            ("script", scriptPath)
        );

        object? scriptInstance = await scriptService.LoadScriptAsync(scriptPath);

        if (scriptInstance != null)
        {
            // Initialize script with world
            scriptService.InitializeScript(scriptInstance, world);

            // Register script instance in the registry
            behaviorRegistry.RegisterScript(typeId, scriptInstance);

            logger.LogWorkflowStatus(
                "Tile behavior ready",
                ("behavior", typeId),
                ("script", scriptPath)
            );
            return true;
        }

        logger.LogError(
            "✗ Failed to compile script for {TypeId}: {Script}",
            typeId,
            scriptPath
        );
        return false;
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
            List<TileBehaviorEntity> modBehaviors = await gameDataContext.TileBehaviors
                .Where(b => b.SourceMod != null && b.BehaviorScript != null)
                .ToListAsync();

            foreach (TileBehaviorEntity modBehavior in modBehaviors)
            {
                string typeId = modBehavior.TileBehaviorId.Value;

                // Skip if already registered (filesystem took precedence)
                if (behaviorRegistry.HasScript(typeId))
                {
                    logger.LogDebug("Mod tile behavior {TypeId} already has script registered", typeId);
                    continue;
                }

                // Register the definition in TypeRegistry if not present
                if (!behaviorRegistry.Contains(typeId))
                {
                    // Create TileBehaviorDefinition from EF Core entity
                    var definition = new TileBehaviorDefinition
                    {
                        DefinitionId = typeId,
                        Name = modBehavior.Name,
                        Description = modBehavior.Description,
                        BehaviorScript = modBehavior.BehaviorScript,
                        Flags = (TileBehaviorFlags)modBehavior.Flags
                    };
                    behaviorRegistry.Register(definition);
                    logger.LogDebug("Registered mod tile behavior definition: {TypeId}", typeId);
                }

                // Compile and register the script
                if (!string.IsNullOrEmpty(modBehavior.BehaviorScript))
                {
                    bool success = await CompileAndRegisterScriptAsync(typeId, modBehavior.BehaviorScript);
                    if (success)
                    {
                        compiledCount++;
                        logger.LogInformation(
                            "Compiled mod tile behavior script: {TypeId} from {Mod}",
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
