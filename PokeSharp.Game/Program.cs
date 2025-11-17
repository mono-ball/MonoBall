using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Services;
using PokeSharp.Game.Systems.Services;

// Ensure glyph-heavy logging renders correctly
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// Enable Windows ANSI color support (required for Spectre.Console colors via Serilog)
if (OperatingSystem.IsWindows())
{
    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
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
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? "Production";

// Load configuration from appsettings.json
var basePath = AppDomain.CurrentDomain.BaseDirectory;
var configuration = SerilogConfiguration.LoadConfiguration(basePath, environment);

// Setup DI container
var services = new ServiceCollection();

try
{
    // Add game services with Serilog logging configuration
    // This replaces the old ConsoleLoggerFactory setup
    services.AddGameServices(configuration, environment);

    // Add the game itself using options pattern
    services.AddSingleton<PokeSharpGame>(sp =>
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var options = new PokeSharpGameOptions
        {
            World = sp.GetRequiredService<Arch.Core.World>(),
            SystemManager = sp.GetRequiredService<PokeSharp.Engine.Systems.Management.SystemManager>(),
            EntityFactory = sp.GetRequiredService<IEntityFactoryService>(),
            ScriptService = sp.GetRequiredService<ScriptService>(),
            BehaviorRegistry = sp.GetRequiredService<TypeRegistry<BehaviorDefinition>>(),
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
            TemplateCacheInitializer = sp.GetRequiredService<TemplateCacheInitializer>()
        };
        return new PokeSharpGame(loggerFactory, options);
    });

    // Build service provider
    var serviceProvider = services.BuildServiceProvider();

    // Get logger to log startup
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("PokeSharp starting | environment: {Environment}, config: {BasePath}", environment, basePath);

    // Create and run the game
    try
    {
        using var game = serviceProvider.GetRequiredService<PokeSharpGame>();
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
        Serilog.Log.CloseAndFlush();
    }
}
catch (Exception ex)
{
    // Log to console if Serilog isn't available yet
    Console.Error.WriteLine($"Fatal error during service registration: {ex}");
    Console.Error.WriteLine(ex.StackTrace);
    throw;
}
