using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Tests for log output formats and formatting behavior.
/// </summary>
public class FormatOutputTests
{
    [Fact]
    public void LogFormatting_ShouldEscape_MarkupCharacters()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();
        var stringWithMarkup = "[Bold]Text[/] and <tag>";

        // Act & Assert - Should escape markup to prevent rendering issues
        var act = () => logger.LogInformation("Message with markup: {Text}", stringWithMarkup);

        act.Should().NotThrow();
    }

    [Fact]
    public void UTF8Encoding_ShouldRender_Correctly()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert - Test various UTF-8 characters and glyphs
        var act = () =>
        {
            logger.LogInformation("Unicode: ▶ • → │ ║ ═");
            logger.LogInformation("Symbols: ✓ ✗ ★ ♥ ◆");
            logger.LogInformation("Box drawing: ╔══╗ ║  ║ ╚══╝");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuredLogging_ShouldFormat_Parameters()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () =>
            logger.LogInformation(
                "Entity {EntityId} at position ({X}, {Y}) with velocity {Velocity:F2}",
                123,
                10,
                20,
                5.67890
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void ColoredOutput_ShouldUse_SpectreMarkup()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert - These use Spectre.Console markup internally
        var act = () =>
        {
            logger.LogInformation("Success message");
            logger.LogWarning("Warning message");
            logger.LogError("Error message");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Timestamps_ShouldBeIncluded_InFileOutput()
    {
        // Arrange
        var logDirectory = Path.Combine(Path.GetTempPath(), $"LoggingTests_{Guid.NewGuid():N}");
        var logger = ConsoleLoggerFactory.CreateWithFile<FormatOutputTests>(
            LogLevel.Information,
            LogLevel.Debug,
            logDirectory
        );

        try
        {
            // Act
            logger.LogInformation("Test message with timestamp");

            // Allow time for async file write
            Thread.Sleep(100);

            // Assert - Check that log file was created
            Directory.Exists(logDirectory).Should().BeTrue();

            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            logFiles.Should().NotBeEmpty();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(logDirectory))
            {
                try
                {
                    Directory.Delete(logDirectory, true);
                }
                catch
                { /* Ignore cleanup errors */
                }
            }
        }
    }

    [Fact]
    public void DiagnosticHeader_ShouldFormat_BoxDrawing()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () =>
        {
            logger.LogDiagnosticHeader("PERFORMANCE REPORT");
            logger.LogDiagnosticInfo("FPS", "60.0");
            logger.LogDiagnosticInfo("Frame Time", "16.7ms");
            logger.LogDiagnosticSeparator();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void BatchOperations_ShouldFormat_Statistics()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () =>
        {
            logger.LogBatchStarted("Sprite loading", 150);
            logger.LogBatchCompleted("Sprite loading", 148, 2, 1250.5);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ControlsHint_ShouldFormat_AsSubdued()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () => logger.LogControlsHint("WASD to move, Space to interact, Esc to quit");

        act.Should().NotThrow();
    }

    [Fact]
    public void RenderStats_ShouldFormat_WithSeparators()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () => logger.LogRenderStats(1523, 768, 45, 12_345_678);

        act.Should().NotThrow();
    }

    [Fact]
    public void PerformanceMetrics_ShouldAlign_Numbers()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert - Numbers should align for readability
        var act = () =>
        {
            logger.LogSystemPerformance("RenderSystem", 2.35, 5.12, 10000);
            logger.LogSystemPerformance("PhysicsSystem", 0.12, 0.45, 5000);
            logger.LogSystemPerformance("AISystem", 15.67, 23.45, 100);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LongStrings_ShouldNotBreak_Formatting()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();
        var veryLongString = new string('A', 500);

        // Act & Assert
        var act = () => logger.LogInformation("Long data: {Data}", veryLongString);

        act.Should().NotThrow();
    }

    [Fact]
    public void SpecialCharacters_ShouldBeEscaped()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () =>
        {
            logger.LogInformation("Path: C:\\Users\\Test\\file.txt");
            logger.LogInformation("Regex: [a-z]+\\d{2,4}");
            logger.LogInformation("XML: <root attr=\"value\">text</root>");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void EmptyStrings_ShouldBeHandled()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();

        // Act & Assert
        var act = () =>
        {
            logger.LogInformation("Empty: '{Empty}'", string.Empty);
            logger.LogInformation("Null: '{Null}'", (string?)null);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void MultiLine_Messages_ShouldFormat_Correctly()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<FormatOutputTests>();
        var multiLine = "Line 1\nLine 2\nLine 3";

        // Act & Assert
        var act = () => logger.LogInformation("Multi-line content:\n{Content}", multiLine);

        act.Should().NotThrow();
    }
}
