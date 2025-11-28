using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug;
using PokeSharp.Engine.Debug.Systems;
using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that initializes the debug console system.
///     Console is managed as a scene that gets pushed onto the scene stack.
///     Follows the standard pipeline step pattern with parameterless constructor.
/// </summary>
public class InitializeConsoleStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializeConsoleStep" /> class.
    /// </summary>
    public InitializeConsoleStep()
        : base(
            "Initializing debug console...",
            InitializationProgress.Complete,
            InitializationProgress.Complete
        ) { }

    /// <inheritdoc />
    protected override Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<InitializeConsoleStep> logger =
            context.LoggerFactory.CreateLogger<InitializeConsoleStep>();

        try
        {
            // Get SceneManager from context (should be available at this stage)
            if (context.SceneManager == null)
            {
                logger.LogWarning(
                    "SceneManager not available in context - debug console will not be available"
                );
                return Task.CompletedTask;
            }

            // Get console system factory from DI (if available)
            ConsoleSystemFactory? consoleSystemFactory =
                context.Services.GetService<ConsoleSystemFactory>();
            if (consoleSystemFactory == null)
            {
                logger.LogWarning(
                    "ConsoleSystemFactory not found in DI container - debug console will not be available"
                );
                return Task.CompletedTask;
            }

            // Create console system using factory (GraphicsDevice and SceneManager are now available)
            ConsoleSystem consoleSystem = consoleSystemFactory.Create(
                context.GraphicsDevice,
                context.SceneManager
            );

            logger.LogInformation("ConsoleSystem created, registering with SystemManager...");

            // Register only as update system (rendering is handled by ConsoleScene)
            context.SystemManager.RegisterUpdateSystem(consoleSystem);

            // Manually initialize since SystemManager.Initialize() was already called
            consoleSystem.Initialize(context.World);

            logger.LogInformation("=== DEBUG CONSOLE READY === Press ~ or ` to toggle");
            logger.LogInformation(
                "Console is managed as a scene - input blocking handled by SceneManager"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FAILED to initialize debug console - check exception details");
            // Don't rethrow - console is optional and game should continue without it
        }

        return Task.CompletedTask;
    }
}
