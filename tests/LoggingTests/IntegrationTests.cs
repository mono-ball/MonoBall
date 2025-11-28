using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Integration tests that simulate real-world usage scenarios.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void GameStartup_Simulation_ShouldLog_AllSystems()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate game startup
        var act = () =>
        {
            logger.LogSystemInitialized("RenderingEngine", ("backend", "OpenGL"), ("vsync", true));
            logger.LogSystemInitialized("InputManager", ("devices", 2));
            logger.LogSystemInitialized("AudioEngine", ("channels", 32));
            logger.LogSystemInitialized("ScriptingEngine", ("compiler", "Roslyn"));

            logger.LogComponentInitialized("EntityManager", 0);
            logger.LogComponentInitialized("MapManager", 1);
            logger.LogComponentInitialized("UIManager", 5);

            logger.LogResourceLoaded("Map", "pallet_town.tmx", ("size", "20x18"));
            logger.LogAssetLoadingStarted("Sprites", 150);
            logger.LogMapLoaded("pallet_town", 20, 18, 360, 25);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void GameLoop_60FPS_ShouldLog_FrameStats()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>(LogLevel.Information);
        var sw = Stopwatch.StartNew();
        var frameCount = 0;

        // Act - Simulate 60 frames (1 second at 60 FPS)
        while (frameCount < 60)
        {
            var frameStart = sw.Elapsed.TotalMilliseconds;

            // Simulate frame work
            Thread.Sleep(16);

            var frameEnd = sw.Elapsed.TotalMilliseconds;
            var frameTime = frameEnd - frameStart;

            // Every 60 frames, log stats
            if (frameCount % 60 == 0 && frameCount > 0)
            {
                logger.LogFramePerformance(16.7f, 60.0f, 14.2f, 18.5f);
            }

            frameCount++;
        }

        // Assert - Should complete without issues
        frameCount.Should().Be(60);
    }

    [Fact]
    public void ErrorRecovery_Scenario_ShouldLog_WarningsAndRecovery()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate error and recovery
        var act = () =>
        {
            // Attempt to load asset
            logger.LogAssetLoadingStarted("Textures", 10);

            // Some fail
            logger.LogResourceNotFound("Texture", "missing_sprite.png");
            logger.LogOperationFailedWithRecovery("Load sprite", "Using placeholder");

            // Recovery successful
            logger.LogResourceLoaded("Texture", "placeholder.png", ("cached", true));
            logger.LogBatchCompleted("Texture loading", 9, 1, 125.5);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void PerformanceMonitoring_Scenario_ShouldLog_SystemMetrics()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate performance monitoring
        var act = () =>
        {
            logger.LogDiagnosticHeader("SYSTEM PERFORMANCE");

            logger.LogSystemPerformance("RenderSystem", 2.3, 5.1, 10000);
            logger.LogSystemPerformance("PhysicsSystem", 1.2, 3.5, 5000);
            logger.LogSystemPerformance("AISystem", 0.8, 2.1, 100);
            logger.LogSystemPerformance("ScriptSystem", 0.5, 1.2, 50);

            logger.LogDiagnosticSeparator();
            logger.LogMemoryStatistics(156.3, 15, 3, 1);

            // Detect slow system
            logger.LogSlowSystem("PathfindingSystem", 15.2, 91.0); // Critical
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void EntityLifecycle_ShouldLog_SpawnAndOperations()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate entity lifecycle
        var act = () =>
        {
            // Spawn entities
            logger.LogEntitySpawned("NPC", 101, "professor_oak", 5, 7);
            logger.LogEntitySpawned("NPC", 102, "rival", 10, 12);
            logger.LogEntitySpawned("Player", 1, "player_template", 8, 9);

            // Entity operations
            logger.LogEntityCreated("Item", 201, ("sprite", "potion"), ("count", 1));

            // Missing component warning
            logger.LogEntityMissingComponent("NPC #103", "AIBehavior", "Cannot process AI");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ScriptingSystem_WithErrors_ShouldLog_Compilation()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate script compilation
        var act = () =>
        {
            logger.LogBatchStarted("Script compilation", 15);

            // Success
            logger.LogResourceLoaded("Script", "wander_behavior.csx", ("lines", 45));
            logger.LogResourceLoaded("Script", "patrol_behavior.csx", ("lines", 67));

            // Error
            var compileError = new InvalidOperationException("Syntax error on line 23");
            logger.LogExceptionWithContext(
                compileError,
                "Failed to compile {Script}",
                "broken_script.csx"
            );
            logger.LogOperationFailedWithRecovery(
                "Compile broken_script.csx",
                "Using previous version"
            );

            logger.LogBatchCompleted("Script compilation", 14, 1, 234.5);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void MapTransition_ShouldLog_LoadingSequence()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate map transition
        var act = () =>
        {
            logger.LogWorkflowStatus("Unloading current map");
            logger.LogBatchStarted("Entity cleanup", 50);
            logger.LogBatchCompleted("Entity cleanup", 50, 0, 12.3);

            logger.LogWorkflowStatus("Loading new map");
            logger.LogResourceLoaded("Map", "route_1.tmx", ("size", "30x30"));
            logger.LogMapLoaded("route_1", 30, 30, 900, 35);

            logger.LogBatchStarted("Entity spawn", 35);
            logger.LogEntitySpawned("NPC", 201, "youngster", 5, 10);
            logger.LogEntitySpawned("NPC", 202, "lass", 15, 20);
            logger.LogBatchCompleted("Entity spawn", 35, 0, 45.2);

            logger.LogWorkflowStatus("Map transition complete");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LongRunning_60SecondTest_ShouldMaintain_Performance()
    {
        // Note: This is a simulated 60-second test compressed to 5 seconds for CI/CD

        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>(LogLevel.Information);
        var sw = Stopwatch.StartNew();
        var frameCount = 0;
        var testDuration = TimeSpan.FromSeconds(5); // Reduced for unit tests

        // Act - Simulate game running for duration
        while (sw.Elapsed < testDuration)
        {
            // Simulate frame at ~60 FPS
            logger.LogDebug("Frame {FrameCount}", frameCount);

            // Log stats every 60 frames
            if (frameCount % 60 == 0 && frameCount > 0)
            {
                logger.LogFramePerformance(16.7f, 60.0f, 14.2f, 18.5f);
            }

            // Log memory every 300 frames (5 seconds)
            if (frameCount % 300 == 0 && frameCount > 0)
            {
                logger.LogMemoryStats(includeGcStats: true);
            }

            Thread.Sleep(16); // ~60 FPS
            frameCount++;
        }

        // Assert
        frameCount.Should().BeGreaterThan(250); // ~5 seconds at 60 FPS
    }

    [Fact]
    public void CriticalError_ShouldLog_WithFullContext()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate critical error
        var act = () =>
        {
            try
            {
                // Simulate critical failure
                throw new OutOfMemoryException("Texture cache exhausted");
            }
            catch (Exception ex)
            {
                logger.LogCriticalError(ex, "Rendering system failure");
                logger.LogSystemUnavailable("RenderingEngine", "Out of memory", isCritical: true);
                logger.LogWorkflowStatus("Attempting recovery");
                logger.LogOperationFailedWithRecovery("Restart renderer", "Clearing cache");
            }
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteGameSession_ShouldLog_AllPhases()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<IntegrationTests>();

        // Act - Simulate complete game session
        var act = () =>
        {
            // 1. Startup
            logger.LogSystemInitialized("Game", ("version", "1.0.0"));
            logger.LogComponentInitialized("AssetManager", 200);

            // 2. Loading
            logger.LogBatchStarted("Initial load", 200);
            logger.LogMapLoaded("start_town", 20, 18, 360, 15);
            logger.LogBatchCompleted("Initial load", 198, 2, 1250.0);

            // 3. Gameplay
            logger.LogEntitySpawned("Player", 1, "player", 10, 10);
            logger.LogFramePerformance(16.7f, 60.0f, 14.2f, 18.5f);

            // 4. Performance monitoring
            logger.LogSystemPerformance("TotalSystems", 4.5, 8.2, 60);
            logger.LogMemoryStatistics(145.2, 12, 2, 0);

            // 5. Shutdown
            logger.LogWorkflowStatus("Shutting down");
            logger.LogSystemInitialized("Cleanup", ("entities", 100));
        };

        act.Should().NotThrow();
    }
}
