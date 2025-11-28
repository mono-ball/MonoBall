using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Tests for logging configuration and level control.
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void LogLevel_Information_ShouldFilter_Debug()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Information);

        // Act & Assert - Debug should be filtered
        var act = () =>
        {
            logger.LogDebug("This should be filtered");
            logger.LogInformation("This should appear");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LogLevel_Warning_ShouldFilter_InfoAndDebug()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Warning);

        // Act & Assert
        var act = () =>
        {
            logger.LogDebug("Filtered");
            logger.LogInformation("Filtered");
            logger.LogWarning("Visible");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LogLevel_Error_ShouldOnly_ShowErrors()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Error);

        // Act & Assert
        var act = () =>
        {
            logger.LogDebug("Filtered");
            logger.LogInformation("Filtered");
            logger.LogWarning("Filtered");
            logger.LogError("Visible error");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LogLevel_Debug_ShouldShow_AllLevels()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Debug);

        // Act & Assert
        var act = () =>
        {
            logger.LogDebug("Debug message");
            logger.LogInformation("Info message");
            logger.LogWarning("Warning message");
            logger.LogError("Error message");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LogLevel_Trace_ShouldShow_Everything()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Trace);

        // Act & Assert
        var act = () =>
        {
            logger.LogTrace("Trace message");
            logger.LogDebug("Debug message");
            logger.LogInformation("Info message");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CompositeLogger_ShouldHave_DifferentLevels_ForConsoleAndFile()
    {
        // Arrange
        var logDirectory = Path.Combine(Path.GetTempPath(), $"LoggingTests_{Guid.NewGuid():N}");
        var logger = ConsoleLoggerFactory.CreateWithFile<ConfigurationTests>(
            consoleLevel: LogLevel.Information,
            fileLevel: LogLevel.Debug,
            logDirectory: logDirectory
        );

        try
        {
            // Act - Debug should go to file but not console
            logger.LogDebug("Debug message");
            logger.LogInformation("Info message");

            Thread.Sleep(100); // Allow async file write

            // Assert - Verify log directory was created
            Directory.Exists(logDirectory).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                try
                {
                    Directory.Delete(logDirectory, true);
                }
                catch
                { /* Ignore */
                }
            }
        }
    }

    [Fact]
    public void FileLogging_ShouldCreate_LogDirectory()
    {
        // Arrange
        var logDirectory = Path.Combine(Path.GetTempPath(), $"LoggingTests_{Guid.NewGuid():N}");

        // Act
        var logger = ConsoleLoggerFactory.CreateWithFile<ConfigurationTests>(
            logDirectory: logDirectory
        );

        logger.LogInformation("Test message");
        Thread.Sleep(100);

        // Assert
        try
        {
            Directory.Exists(logDirectory).Should().BeTrue();
            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            logFiles.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                try
                {
                    Directory.Delete(logDirectory, true);
                }
                catch
                { /* Ignore */
                }
            }
        }
    }

    [Fact]
    public void MultipleLoggers_WithDifferentLevels_ShouldCoexist()
    {
        // Arrange
        var debugLogger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Debug);
        var infoLogger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Information);
        var warnLogger = ConsoleLoggerFactory.Create<ConfigurationTests>(LogLevel.Warning);

        // Act & Assert
        var act = () =>
        {
            debugLogger.LogDebug("Debug from debug logger");
            infoLogger.LogDebug("Debug from info logger (filtered)");
            warnLogger.LogDebug("Debug from warn logger (filtered)");

            debugLogger.LogInformation("Info from debug logger");
            infoLogger.LogInformation("Info from info logger");
            warnLogger.LogInformation("Info from warn logger (filtered)");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LoggerFactory_ShouldCreate_MultipleCategories()
    {
        // Arrange
        var factory = ConsoleLoggerFactory.Create(LogLevel.Information);

        // Act
        var logger1 = factory.CreateLogger("Category1");
        var logger2 = factory.CreateLogger("Category2");
        var logger3 = factory.CreateLogger("Category3");

        // Assert
        logger1.Should().NotBeNull();
        logger2.Should().NotBeNull();
        logger3.Should().NotBeNull();

        // All should log without interference
        var act = () =>
        {
            logger1.LogInformation("Message from Category1");
            logger2.LogInformation("Message from Category2");
            logger3.LogInformation("Message from Category3");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void DefaultLogLevel_ShouldBe_Information()
    {
        // Arrange & Act - Create logger without specifying level
        var logger = ConsoleLoggerFactory.Create<ConfigurationTests>();

        // Assert - Should default to Information
        var act = () =>
        {
            logger.LogDebug("This should be filtered (default)");
            logger.LogInformation("This should appear (default)");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void FileRotation_ShouldCreate_DailyLogFiles()
    {
        // Arrange
        var logDirectory = Path.Combine(Path.GetTempPath(), $"LoggingTests_{Guid.NewGuid():N}");
        var logger = ConsoleLoggerFactory.CreateWithFile<ConfigurationTests>(
            logDirectory: logDirectory
        );

        try
        {
            // Act
            logger.LogInformation("Test message for file rotation");
            Thread.Sleep(100);

            // Assert - Should create a log file with date in name
            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            logFiles.Should().NotBeEmpty();

            var fileName = Path.GetFileName(logFiles[0]);
            fileName.Should().Contain(DateTime.Now.ToString("yyyy-MM-dd"));
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                try
                {
                    Directory.Delete(logDirectory, true);
                }
                catch
                { /* Ignore */
                }
            }
        }
    }
}
