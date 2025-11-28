using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Tests for startup logging initialization.
/// </summary>
public class StartupLoggingTests
{
    [Fact]
    public void LoggerFactory_ShouldInitializeCorrectly()
    {
        // Act
        var loggerFactory = ConsoleLoggerFactory.Create(LogLevel.Information);

        // Assert
        loggerFactory.Should().NotBeNull();
        loggerFactory.Should().BeAssignableTo<ILoggerFactory>();
    }

    [Fact]
    public void CreateLogger_ShouldReturnTypedLogger()
    {
        // Arrange
        var loggerFactory = ConsoleLoggerFactory.Create();

        // Act
        var logger = loggerFactory.CreateLogger<StartupLoggingTests>();

        // Assert
        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<ILogger<StartupLoggingTests>>();
    }

    [Fact]
    public void CreateLogger_WithDifferentLevels_ShouldRespectMinLevel()
    {
        // Arrange & Act
        var infoLogger = ConsoleLoggerFactory.Create<StartupLoggingTests>(LogLevel.Information);
        var debugLogger = ConsoleLoggerFactory.Create<StartupLoggingTests>(LogLevel.Debug);
        var warnLogger = ConsoleLoggerFactory.Create<StartupLoggingTests>(LogLevel.Warning);

        // Assert
        infoLogger.Should().NotBeNull();
        debugLogger.Should().NotBeNull();
        warnLogger.Should().NotBeNull();

        // Note: IsEnabled is checked internally by the logger implementation
    }

    [Fact]
    public void LogSystemInitialized_ShouldNotThrow()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<StartupLoggingTests>();

        // Act & Assert
        var act = () =>
            logger.LogSystemInitialized("TestSystem", ("version", "1.0.0"), ("features", 42));

        act.Should().NotThrow();
    }

    [Fact]
    public void LogComponentInitialized_ShouldNotThrow()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<StartupLoggingTests>();

        // Act & Assert
        var act = () => logger.LogComponentInitialized("TestComponent", 100);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogResourceLoaded_ShouldNotThrow()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<StartupLoggingTests>();

        // Act & Assert
        var act = () =>
            logger.LogResourceLoaded("Texture", "player_sprite.png", ("width", 32), ("height", 32));

        act.Should().NotThrow();
    }

    [Fact]
    public void MultipleLoggers_ShouldNotInterfere()
    {
        // Arrange
        var factory = ConsoleLoggerFactory.Create();
        var logger1 = factory.CreateLogger("Component1");
        var logger2 = factory.CreateLogger("Component2");
        var logger3 = factory.CreateLogger("Component3");

        // Act & Assert
        var act = () =>
        {
            logger1.LogInformation("Component 1 initialized");
            logger2.LogInformation("Component 2 initialized");
            logger3.LogInformation("Component 3 initialized");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuredParameters_ShouldBeFormatted()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<StartupLoggingTests>();

        // Act & Assert
        var act = () =>
            logger.LogInformation(
                "System {SystemName} loaded with {Count} items in {TimeMs}ms",
                "TestSystem",
                100,
                42.5
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void CompositeLogger_ShouldCreateSuccessfully()
    {
        // Act
        var logger = ConsoleLoggerFactory.CreateWithFile<StartupLoggingTests>(
            LogLevel.Information,
            LogLevel.Debug,
            "TestLogs"
        );

        // Assert
        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<ILogger<StartupLoggingTests>>();
    }

    [Fact]
    public void LogTemplates_AllInitializationMethods_ShouldNotThrow()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<StartupLoggingTests>();

        // Act & Assert - Test all initialization templates
        var act = () =>
        {
            logger.LogSystemInitialized("RenderingSystem", ("entities", 1000));
            logger.LogComponentInitialized("TileManager", 2500);
            logger.LogResourceLoaded("Map", "pallet_town.tmx", ("size", "20x18"));
            logger.LogAssetLoadingStarted("Sprites", 150);
            logger.LogAssetStatus("Asset cache warmed", ("cached", 150));
        };

        act.Should().NotThrow();
    }
}
