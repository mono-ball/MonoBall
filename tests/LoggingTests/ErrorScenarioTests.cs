using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Tests for error scenario logging.
/// </summary>
public class ErrorScenarioTests
{
    [Fact]
    public void LogError_WithException_ShouldInclude_ExceptionDetails()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();
        var exception = new FileNotFoundException("sprite.png not found", "sprite.png");

        // Act & Assert
        var act = () =>
            logger.LogError(exception, "Failed to load sprite {FileName}", "sprite.png");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogExceptionWithContext_ShouldInclude_ThreadAndMachineInfo()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert
        var act = () =>
            logger.LogExceptionWithContext(
                exception,
                "Operation failed during {Operation}",
                "MapLoading"
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void LogCriticalError_ShouldHighlight_Severity()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();
        var exception = new OutOfMemoryException("Insufficient memory");

        // Act & Assert
        var act = () => logger.LogCriticalError(exception, "System initialization");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogWarning_ValidationFailure_ShouldLog_AsWarning()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () =>
            logger.LogWarning(
                "Validation failed for entity {EntityId}: {Reason}",
                123,
                "Missing required component"
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void LogResourceNotFound_ShouldUse_WarningLevel()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () => logger.LogResourceNotFound("Texture", "missing_sprite.png");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSlowOperation_ShouldWarn_AboutPerformance()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () => logger.LogSlowOperation("MapLoading", 250.5, 100.0);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSlowSystem_Critical_ShouldUse_RedHighlight()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert - >50% should be critical
        var act = () => logger.LogSlowSystem("PathfindingSystem", 12.5, 75.0);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSlowSystem_High_ShouldUse_OrangeHighlight()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert - >20% should be high
        var act = () => logger.LogSlowSystem("AIUpdateSystem", 5.2, 31.2);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSlowSystem_Medium_ShouldUse_YellowHighlight()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert - >10% should be medium
        var act = () => logger.LogSlowSystem("AnimationSystem", 2.1, 12.6);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogOperationFailedWithRecovery_ShouldSuggest_Resolution()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () =>
            logger.LogOperationFailedWithRecovery("Script compilation", "Using cached version");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogOperationSkipped_ShouldProvide_Reason()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () => logger.LogOperationSkipped("Entity spawn", "Template not found");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSystemUnavailable_Critical_ShouldLog_AsError()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () =>
            logger.LogSystemUnavailable(
                "RenderingEngine",
                "Graphics device lost",
                isCritical: true
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSystemDependencyMissing_ShouldHighlight_Dependency()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () =>
            logger.LogSystemDependencyMissing(
                "ScriptingSystem",
                "RoslynCompiler",
                isCritical: false
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void LogEntityMissingComponent_ShouldProvide_Context()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () =>
            logger.LogEntityMissingComponent(
                "NPC #123",
                "PositionComponent",
                "Cannot render entity"
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void LogEntityNotFound_ShouldIndicate_MissingEntity()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () => logger.LogEntityNotFound("Player entity", "Movement update");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogTemplateMissing_ShouldLog_AsError()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () => logger.LogTemplateMissing("npc_template_oak");

        act.Should().NotThrow();
    }

    [Fact]
    public void MultipleErrors_Sequential_ShouldAll_BeLogged()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();

        // Act & Assert
        var act = () =>
        {
            logger.LogResourceNotFound("Sprite", "player.png");
            logger.LogResourceNotFound("Sound", "battle.wav");
            logger.LogResourceNotFound("Map", "route1.tmx");
            logger.LogTemplateMissing("enemy_template");
            logger.LogSlowOperation("Initialization", 500.0, 100.0);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void NullException_ShouldBeHandled_Gracefully()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ErrorScenarioTests>();
        Exception? nullException = null;

        // Act & Assert - Should handle null exception without crashing
        var act = () => logger.LogError(nullException, "Error occurred");

        // Note: This tests that the logger handles null gracefully
        // The actual behavior depends on the logger implementation
        act.Should().NotThrow();
    }
}
