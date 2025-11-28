using Arch.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Features;
using PokeSharp.Engine.Debug.Logging;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Systems;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Engine.Debug;

/// <summary>
///     Extension methods for registering debug console services in the DI container.
/// </summary>
public static class DebugServiceCollectionExtensions
{
    /// <summary>
    ///     Adds debug console services to the service collection.
    ///     This enables dependency injection for the console system and its components.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration to load console settings from appsettings.json.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDebugConsole(
        this IServiceCollection services,
        IConfiguration? configuration = null
    )
    {
        // Console Logger Provider
        services.AddSingleton<ConsoleLoggerProvider>();

        // Console Configuration - load from appsettings.json or use defaults
        if (configuration != null)
        {
            // Get configuration from appsettings.json using Get<T>() which supports records
            IConfigurationSection consoleConfigSection = configuration.GetSection(
                ConsoleConfig.SectionName
            );
            ConsoleConfig consoleConfig =
                consoleConfigSection.Get<ConsoleConfig>() ?? new ConsoleConfig();

            // Set default theme from config (must be done before ThemeManager is accessed)
            if (!string.IsNullOrEmpty(consoleConfig.Theme))
            {
                ThemeManager.SetDefaultTheme(consoleConfig.Theme);
            }

            // Register as singleton
            services.AddSingleton(consoleConfig);
        }
        else
        {
            // Fallback to hardcoded defaults if no configuration provided
            services.AddSingleton<ConsoleConfig>(sp =>
            {
                return new ConsoleConfig
                {
                    Size = ConsoleSize.Medium,
                    FontSize = 16,
                    SyntaxHighlightingEnabled = true,
                    AutoCompleteEnabled = true,
                    PersistHistory = true,
                    LoggingEnabled = true,
                    MinimumLogLevel = LogLevel.Debug,
                };
            });
        }

        // Console UI Components
        services.AddSingleton<ConsoleCommandHistory>();
        services.AddSingleton<ConsoleHistoryPersistence>(sp =>
        {
            ILogger<ConsoleHistoryPersistence> logger = sp.GetRequiredService<
                ILogger<ConsoleHistoryPersistence>
            >();
            return new ConsoleHistoryPersistence(logger);
        });

        // Script Manager
        services.AddSingleton<ScriptManager>(sp =>
        {
            ILogger<ScriptManager> logger = sp.GetRequiredService<ILogger<ScriptManager>>();
            return new ScriptManager(logger: logger);
        });

        // Alias Manager
        services.AddSingleton<AliasMacroManager>(sp =>
        {
            ILogger<AliasMacroManager> logger = sp.GetRequiredService<ILogger<AliasMacroManager>>();
            ScriptManager scriptManager = sp.GetRequiredService<ScriptManager>();
            string aliasesPath = Path.Combine(scriptManager.ScriptsDirectory, "aliases.txt");
            return new AliasMacroManager(aliasesPath, logger);
        });

        // Bookmark Manager
        services.AddSingleton<BookmarkedCommandsManager>(sp =>
        {
            ILogger<BookmarkedCommandsManager> logger = sp.GetRequiredService<
                ILogger<BookmarkedCommandsManager>
            >();
            ScriptManager scriptManager = sp.GetRequiredService<ScriptManager>();
            string bookmarksPath = Path.Combine(scriptManager.ScriptsDirectory, "bookmarks.txt");
            return new BookmarkedCommandsManager(bookmarksPath, logger);
        });

        // Console Script Evaluator
        services.AddSingleton<ConsoleScriptEvaluator>(sp =>
        {
            ILogger<ConsoleScriptEvaluator> logger = sp.GetRequiredService<
                ILogger<ConsoleScriptEvaluator>
            >();
            return new ConsoleScriptEvaluator(logger);
        });

        // Console Auto-Complete
        services.AddSingleton<ConsoleAutoComplete>(sp =>
        {
            ILogger<ConsoleAutoComplete> logger = sp.GetRequiredService<
                ILogger<ConsoleAutoComplete>
            >();
            return new ConsoleAutoComplete(logger);
        });

        // Console Globals (Script API)
        // Note: GraphicsDevice will be set after it's available
        services.AddSingleton<ConsoleGlobals>(sp =>
        {
            IScriptingApiProvider apiProvider = sp.GetRequiredService<IScriptingApiProvider>();
            World world = sp.GetRequiredService<World>();
            SystemManager systemManager = sp.GetRequiredService<SystemManager>();
            ILogger<ConsoleGlobals> logger = sp.GetRequiredService<ILogger<ConsoleGlobals>>();
            // GraphicsDevice is set later via SetGraphicsDevice
            return new ConsoleGlobals(apiProvider, world, systemManager, null!, logger);
        });

        // Watch Preset Manager
        services.AddSingleton<WatchPresetManager>(sp =>
        {
            ILogger<WatchPresetManager> logger = sp.GetRequiredService<
                ILogger<WatchPresetManager>
            >();
            ScriptManager scriptManager = sp.GetRequiredService<ScriptManager>();
            string presetsPath = Path.Combine(scriptManager.ScriptsDirectory, "watch_presets");
            return new WatchPresetManager(presetsPath, logger);
        });

        // Console System Factory
        // Note: ConsoleSystem and its dependencies that need GraphicsDevice
        // are created via a factory after GraphicsDevice is available
        services.AddSingleton<ConsoleSystemFactory>();

        return services;
    }
}

/// <summary>
///     Factory for creating ConsoleSystem after GraphicsDevice and SceneManager are available.
/// </summary>
public class ConsoleSystemFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ConsoleSystemFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    ///     Creates and initializes the console system with the provided GraphicsDevice and SceneManager.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="sceneManager">The scene manager for pushing console scenes.</param>
    public ConsoleSystem Create(GraphicsDevice graphicsDevice, SceneManager sceneManager)
    {
        // Get dependencies from DI container
        World world = _serviceProvider.GetRequiredService<World>();
        IScriptingApiProvider apiProvider =
            _serviceProvider.GetRequiredService<IScriptingApiProvider>();
        SystemManager systemManager = _serviceProvider.GetRequiredService<SystemManager>();
        ILogger<ConsoleSystem> logger = _serviceProvider.GetRequiredService<
            ILogger<ConsoleSystem>
        >();
        ConsoleLoggerProvider? consoleLoggerProvider =
            _serviceProvider.GetService<ConsoleLoggerProvider>();

        // Create ConsoleSystem with scene manager
        var consoleSystem = new ConsoleSystem(
            world,
            apiProvider,
            graphicsDevice,
            systemManager,
            sceneManager,
            _serviceProvider,
            logger,
            consoleLoggerProvider
        );

        return consoleSystem;
    }
}
