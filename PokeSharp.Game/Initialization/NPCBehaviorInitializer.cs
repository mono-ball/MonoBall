using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
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
    TypeRegistry<BehaviorDefinition> behaviorRegistry)
{
    private readonly ILogger<NPCBehaviorInitializer> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly World _world = world;
    private readonly SystemManager _systemManager = systemManager;
    private readonly ScriptService _scriptService = scriptService;
    private readonly TypeRegistry<BehaviorDefinition> _behaviorRegistry = behaviorRegistry;

    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Load all behavior definitions from JSON
            var loadedCount = _behaviorRegistry.LoadAllAsync().Result;
            _logger.LogInformation("Loaded {Count} behavior definitions", loadedCount);

            // Load and compile behavior scripts for each type
            foreach (var typeId in _behaviorRegistry.GetAllTypeIds())
            {
                var definition = _behaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    _logger.LogInformation(
                        "Loading behavior script for {TypeId}: {Script}",
                        typeId,
                        scripted.BehaviorScript
                    );

                    var scriptInstance = _scriptService.LoadScriptAsync(scripted.BehaviorScript).Result;

                    if (scriptInstance != null)
                    {
                        // Initialize script with world
                        _scriptService.InitializeScript(scriptInstance, _world);

                        // Register script instance in the registry
                        _behaviorRegistry.RegisterScript(typeId, scriptInstance);

                        _logger.LogInformation(
                            "✓ Loaded and initialized behavior: {TypeId}",
                            typeId
                        );
                    }
                    else
                    {
                        _logger.LogError(
                            "✗ Failed to compile script for {TypeId}: {Script}",
                            typeId,
                            scripted.BehaviorScript
                        );
                    }
                }
            }

            // Register NPCBehaviorSystem
            var npcBehaviorLogger = _loggerFactory.CreateLogger<NPCBehaviorSystem>();
            var npcBehaviorSystem = new NPCBehaviorSystem(npcBehaviorLogger, _loggerFactory);
            npcBehaviorSystem.SetBehaviorRegistry(_behaviorRegistry);
            _systemManager.RegisterSystem(npcBehaviorSystem);

            _logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }
}

