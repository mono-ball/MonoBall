using System.Runtime.InteropServices;
using System.Text;
using Arch.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Factories;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Infrastructure.Services;
using MonoBallFramework.Game.Initialization.Factories;
using MonoBallFramework.Game.Initialization.Initializers;
using MonoBallFramework.Game.Input;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;
using Serilog;

// Ensure glyph-heavy logging renders correctly
Console.OutputEncoding = new UTF8Encoding(false);

// Enable Windows ANSI color support (required for Spectre.Console colors via Serilog)
if (OperatingSystem.IsWindows())
{
    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
    if (handle != IntPtr.Zero)
    {
        GetConsoleMode(handle, out uint mode);
        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        SetConsoleMode(handle, mode);
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}

// Determine environment (Development, Production, etc.)
string environment =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? "Production";

// Load configuration from appsettings.json
string basePath = AppDomain.CurrentDomain.BaseDirectory;
IConfiguration configuration = SerilogConfiguration.LoadConfiguration(basePath, environment);

// Setup DI container
var services = new ServiceCollection();

try
{
    // Add game services with Serilog logging configuration
    // This replaces the old ConsoleLoggerFactory setup
    services.AddGameServices(configuration, environment);

    // Add the game itself using options pattern
    services.AddSingleton<MonoBallFrameworkGame>(sp =>
    {
        ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        IOptions<GameConfiguration> gameConfig = sp.GetRequiredService<
            IOptions<GameConfiguration>
        >();
        var options = new MonoBallFrameworkGameOptions
        {
            World = sp.GetRequiredService<World>(),
            SystemManager = sp.GetRequiredService<SystemManager>(),
            EntityFactory = sp.GetRequiredService<IEntityFactoryService>(),
            ScriptService = sp.GetRequiredService<ScriptService>(),
            BehaviorRegistry = sp.GetRequiredService<TypeRegistry<BehaviorDefinition>>(),
            TileBehaviorRegistry = sp.GetRequiredService<TypeRegistry<TileBehaviorDefinition>>(),
            ApiProvider = sp.GetRequiredService<IScriptingApiProvider>(),
            PerformanceMonitor = sp.GetRequiredService<PerformanceMonitor>(),
            InputManager = sp.GetRequiredService<InputManager>(),
            PlayerFactory = sp.GetRequiredService<PlayerFactory>(),
            GameTime = sp.GetRequiredService<IGameTimeService>(),
            PoolManager = sp.GetRequiredService<EntityPoolManager>(),
            DataLoader = sp.GetRequiredService<GameDataLoader>(),
            NpcDefinitionService = sp.GetRequiredService<NpcDefinitionService>(),
            MapDefinitionService = sp.GetRequiredService<MapDefinitionService>(),
            SpriteLoader = sp.GetRequiredService<SpriteLoader>(),
            TemplateCacheInitializer = sp.GetRequiredService<TemplateCacheInitializer>(),
        };
        return new MonoBallFrameworkGame(loggerFactory, options, sp, gameConfig);
    });

    // Build service provider
    ServiceProvider serviceProvider = services.BuildServiceProvider();

    // Get logger to log startup
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "MonoBall Framework starting | environment: {Environment}, config: {BasePath}",
        environment,
        basePath
    );

    // Create and run the game
    try
    {
        using MonoBallFrameworkGame game = serviceProvider.GetRequiredService<MonoBallFrameworkGame>();
        game.Run();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Fatal error during game execution");
        throw;
    }
    finally
    {
        // Ensure all logs are flushed before exit
        Log.CloseAndFlush();
    }
}
catch (Exception ex)
{
    // Log to console if Serilog isn't available yet
    Console.Error.WriteLine($"Fatal error during service registration: {ex}");
    Console.Error.WriteLine(ex.StackTrace);
    throw;
}
