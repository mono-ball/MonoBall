using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Scripting.Systems;

namespace PokeSharp.Game.Initialization.Behaviors;

/// <summary>
///     Initializes the script attachment system for composition-based scripting.
///     Enables multiple scripts to be attached to entities with priority-based execution.
/// </summary>
/// <remarks>
///     Phase 3.2: Multi-Script Composition
///     This initializer registers ScriptAttachmentSystem to manage the lifecycle
///     of ScriptAttachment components, enabling composition patterns where multiple
///     scripts can be attached to the same entity and execute in priority order.
/// </remarks>
public class ScriptAttachmentSystemInitializer(
    ILogger<ScriptAttachmentSystemInitializer> logger,
    ILoggerFactory loggerFactory,
    SystemManager systemManager,
    ScriptService scriptService,
    IScriptingApiProvider apiProvider,
    IEventBus eventBus
)
{
    /// <summary>
    ///     Initializes the script attachment system and registers it with the system manager.
    /// </summary>
    public void Initialize()
    {
        try
        {
            logger.LogWorkflowStatus(
                "Initializing script attachment system",
                ("priority", SystemPriority.ScriptAttachment)
            );

            // Create ScriptAttachmentSystem with all dependencies
            ILogger<ScriptAttachmentSystem> systemLogger =
                loggerFactory.CreateLogger<ScriptAttachmentSystem>();

            var scriptAttachmentSystem = new ScriptAttachmentSystem(
                systemLogger,
                loggerFactory,
                scriptService,
                apiProvider,
                eventBus
            );

            // Register with system manager
            systemManager.RegisterUpdateSystem(scriptAttachmentSystem);

            logger.LogSystemInitialized(
                "ScriptAttachmentSystem",
                ("priority", SystemPriority.ScriptAttachment),
                ("composition", "enabled")
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize script attachment system");
            throw;
        }
    }
}
