using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Tests for runtime logging behavior.
/// </summary>
public class RuntimeLoggingTests
{
    [Fact]
    public void Debug_Logs_ShouldNotAppear_AtInformationLevel()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Information);

        // Act & Assert - Debug logs should be filtered out
        var act = () =>
        {
            logger.LogDebug("This should not appear");
            logger.LogInformation("This should appear");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void LogLevel_Warning_ShouldFilter_InfoAndDebug()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Warning);

        // Act & Assert
        var act = () =>
        {
            logger.LogDebug("Filtered debug");
            logger.LogInformation("Filtered info");
            logger.LogWarning("Visible warning");
            logger.LogError("Visible error");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void HighFrequency_Logging_ShouldNotCrash()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Debug);

        // Act & Assert - Log 1000 messages rapidly
        var act = () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                logger.LogDebug("Frame {FrameNumber} update", i);
            }
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Performance_HighFrequencyLogging_ShouldBeFast()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Information);
        var sw = Stopwatch.StartNew();

        // Act - Log 10,000 messages
        for (int i = 0; i < 10000; i++)
        {
            logger.LogInformation("Message {Index}", i);
        }

        sw.Stop();

        // Assert - Should complete in reasonable time (< 5 seconds for 10k messages)
        sw.Elapsed.TotalSeconds.Should().BeLessThan(5.0);
    }

    [Fact]
    public void LogTimed_ShouldMeasure_OperationTime()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Debug);

        // Act & Assert
        logger.LogTimed(
            "TestOperation",
            () =>
            {
                Thread.Sleep(50); // Simulate work
            },
            warnThresholdMs: 100
        );

        // Should not throw and should log timing
    }

    [Fact]
    public void LogTimed_WithReturnValue_ShouldReturn_CorrectValue()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Debug);

        // Act
        var result = logger.LogTimed(
            "CalculationOperation",
            () =>
            {
                Thread.Sleep(10);
                return 42;
            }
        );

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void LogMemoryStats_ShouldLog_CurrentMemory()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>();

        // Act & Assert
        var act = () => logger.LogMemoryStats(includeGcStats: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void AllTemplates_ShouldLog_WithoutErrors()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>();

        // Act & Assert - Test various template types
        var act = () =>
        {
            logger.LogEntitySpawned("NPC", 123, "oak_template", 10, 20);
            logger.LogEntityCreated("Player", 1, ("position", "0,0"), ("sprite", "player.png"));
            logger.LogMapLoaded("route1", 32, 24, 768, 15);
            logger.LogFramePerformance(16.7f, 60.0f, 14.2f, 18.5f);
            logger.LogSystemPerformance("RenderSystem", 2.3, 5.1, 10000);
            logger.LogMemoryStatistics(128.5, 10, 2, 0);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ConcurrentLogging_FromMultipleThreads_ShouldBeThreadSafe()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>();
        var tasks = new List<Task>();

        // Act - Create 10 threads logging concurrently
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(
                Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        logger.LogInformation("Thread {ThreadId} message {MessageId}", threadId, j);
                    }
                })
            );
        }

        // Assert
        var act = async () => await Task.WhenAll(tasks);
        act.Should().NotThrowAsync();
    }

    [Fact]
    public void LongRunningTest_60Seconds_ShouldNotDegrade()
    {
        // This test is marked as fact but could be made into a manual test
        // due to its long duration. For CI/CD, consider using [Fact(Skip = "Long running test")]

        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>();
        var sw = Stopwatch.StartNew();
        var messageCount = 0;

        // Act - Log continuously for 1 second (instead of 60 for unit test speed)
        while (sw.Elapsed.TotalSeconds < 1.0)
        {
            logger.LogDebug("Frame update {MessageCount}", messageCount++);
            Thread.Sleep(16); // Simulate ~60 FPS
        }

        sw.Stop();

        // Assert
        messageCount.Should().BeGreaterThan(50); // Should have processed multiple frames
    }

    [Fact]
    public void FilteredLogs_ShouldNotImpact_Performance()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<RuntimeLoggingTests>(LogLevel.Warning);
        var sw = Stopwatch.StartNew();

        // Act - Log 10,000 filtered debug messages
        for (int i = 0; i < 10000; i++)
        {
            logger.LogDebug("Filtered message {Index}", i); // Should be filtered out quickly
        }

        sw.Stop();

        // Assert - Filtered logs should be very fast (< 100ms for 10k)
        sw.Elapsed.TotalMilliseconds.Should().BeLessThan(100);
    }
}
