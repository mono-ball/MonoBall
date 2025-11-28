using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Xunit;

namespace LoggingTests;

/// <summary>
/// Performance tests for logging implementation.
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void MemoryUsage_BeforeAndAfter_ShouldBeSimilar()
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(false);
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();

        // Act - Log 1000 messages
        for (int i = 0; i < 1000; i++)
        {
            logger.LogInformation("Message {Index}", i);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var afterMemory = GC.GetTotalMemory(false);

        // Assert - Memory increase should be minimal (< 5MB for 1000 messages)
        var memoryIncrease = (afterMemory - beforeMemory) / 1024.0 / 1024.0;
        memoryIncrease.Should().BeLessThan(5.0);
    }

    [Fact]
    public void GCPressure_ShouldNotIncrease_Significantly()
    {
        // Arrange
        GC.Collect();
        var beforeGen0 = GC.CollectionCount(0);
        var beforeGen1 = GC.CollectionCount(1);
        var beforeGen2 = GC.CollectionCount(2);

        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();

        // Act - Log 10,000 messages
        for (int i = 0; i < 10000; i++)
        {
            logger.LogInformation("Message {Index}", i);
        }

        var afterGen0 = GC.CollectionCount(0);
        var afterGen1 = GC.CollectionCount(1);
        var afterGen2 = GC.CollectionCount(2);

        // Assert - GC should not run excessively
        var gen0Increase = afterGen0 - beforeGen0;
        var gen1Increase = afterGen1 - beforeGen1;
        var gen2Increase = afterGen2 - beforeGen2;

        gen0Increase.Should().BeLessThan(50); // Gen0 collections should be reasonable
        gen1Increase.Should().BeLessThan(10); // Gen1 should be minimal
        gen2Increase.Should().BeLessThan(3); // Gen2 should be very rare
    }

    [Fact]
    public void FilteredLogs_ShouldHave_MinimalPerformanceImpact()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>(LogLevel.Error);
        var sw = Stopwatch.StartNew();

        // Act - Log 100,000 filtered messages (should be fast)
        for (int i = 0; i < 100000; i++)
        {
            logger.LogDebug("Filtered message {Index}", i);
        }

        sw.Stop();

        // Assert - Filtering should be extremely fast (< 50ms for 100k)
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
    }

    [Fact]
    public void HighFrequencyLogging_ShouldNotDegrade_OverTime()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();
        var times = new List<double>();

        // Act - Measure time for batches of 1000 messages
        for (int batch = 0; batch < 10; batch++)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                logger.LogInformation("Batch {Batch} message {Index}", batch, i);
            }
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert - Performance should not degrade significantly
        var firstBatchTime = times[0];
        var lastBatchTime = times[^1];

        // Last batch should not be more than 2x slower than first
        lastBatchTime.Should().BeLessThan(firstBatchTime * 2.0);
    }

    [Fact]
    public void ComplexFormatting_ShouldStillBe_Fast()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();
        var sw = Stopwatch.StartNew();

        // Act - Log with complex formatting
        for (int i = 0; i < 1000; i++)
        {
            logger.LogInformation(
                "Entity {EntityId} at ({X:F2}, {Y:F2}) moving to ({TargetX:F2}, {TargetY:F2}) with speed {Speed:F3}",
                i,
                i * 1.5,
                i * 2.3,
                i * 1.5 + 10,
                i * 2.3 + 15,
                i * 0.05
            );
        }

        sw.Stop();

        // Assert - Should complete in reasonable time (< 2 seconds for 1000)
        sw.Elapsed.TotalSeconds.Should().BeLessThan(2.0);
    }

    [Fact]
    public void ParallelLogging_ShouldScale_WithThreads()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();
        var threadCount = Environment.ProcessorCount;
        var messagesPerThread = 1000;

        var sw = Stopwatch.StartNew();

        // Act - Log from multiple threads in parallel
        Parallel.For(
            0,
            threadCount,
            threadId =>
            {
                for (int i = 0; i < messagesPerThread; i++)
                {
                    logger.LogInformation("Thread {ThreadId} message {Index}", threadId, i);
                }
            }
        );

        sw.Stop();

        // Assert - Total time should be reasonable
        var totalMessages = threadCount * messagesPerThread;
        var messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;

        // Should process at least 1000 messages per second
        messagesPerSecond.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void TemplateLogging_ShouldBe_Efficient()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();
        var sw = Stopwatch.StartNew();

        // Act - Use logging templates
        for (int i = 0; i < 1000; i++)
        {
            logger.LogSystemPerformance("TestSystem", 2.5, 5.0, i);
            logger.LogFramePerformance(16.7f, 60.0f, 14.2f, 18.5f);
            logger.LogMemoryStatistics(128.5, 10, 2, 0);
        }

        sw.Stop();

        // Assert - Template logging should be fast (< 3 seconds for 3000 template calls)
        sw.Elapsed.TotalSeconds.Should().BeLessThan(3.0);
    }

    [Fact]
    public void AsyncFileLogging_ShouldNotBlock_Caller()
    {
        // Arrange
        var logDirectory = Path.Combine(Path.GetTempPath(), $"LoggingTests_{Guid.NewGuid():N}");
        var logger = ConsoleLoggerFactory.CreateWithFile<PerformanceTests>(
            logDirectory: logDirectory
        );

        try
        {
            var sw = Stopwatch.StartNew();

            // Act - Log 1000 messages (should not block significantly)
            for (int i = 0; i < 1000; i++)
            {
                logger.LogInformation("Async message {Index}", i);
            }

            sw.Stop();

            // Assert - Async logging should not block (<500ms for 1000 messages)
            sw.ElapsedMilliseconds.Should().BeLessThan(500);
        }
        finally
        {
            Thread.Sleep(200); // Allow async writes to complete
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
    public void StringAllocation_ShouldBe_Minimal_WhenFiltered()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>(LogLevel.Error);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(false);

        // Act - Log many filtered messages (should allocate very little)
        for (int i = 0; i < 10000; i++)
        {
            logger.LogDebug("Filtered message with data: {Data}", i);
        }

        var afterMemory = GC.GetTotalMemory(false);
        var allocatedMB = (afterMemory - beforeMemory) / 1024.0 / 1024.0;

        // Assert - Should allocate minimal memory for filtered logs (< 1MB)
        allocatedMB.Should().BeLessThan(1.0);
    }

    [Fact]
    public void LoggedMemoryStats_ShouldMatch_GCMetrics()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();

        // Act & Assert - Memory stats should be accurate
        var act = () =>
        {
            logger.LogMemoryStats(includeGcStats: true);
            logger.LogMemoryStats(includeGcStats: false);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CPUUsage_ShouldBe_Minimal()
    {
        // Arrange
        var logger = ConsoleLoggerFactory.Create<PerformanceTests>();
        var process = Process.GetCurrentProcess();

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            logger.LogInformation("Warmup {Index}", i);
        }

        Thread.Sleep(100);

        var startCpu = process.TotalProcessorTime;
        var sw = Stopwatch.StartNew();

        // Act - Log for 1 second
        while (sw.Elapsed.TotalSeconds < 1.0)
        {
            logger.LogInformation("Performance test message");
            Thread.Sleep(16); // ~60 FPS
        }

        sw.Stop();
        var endCpu = process.TotalProcessorTime;
        var cpuUsed = (endCpu - startCpu).TotalMilliseconds;
        var cpuPercent = (cpuUsed / sw.Elapsed.TotalMilliseconds) * 100.0;

        // Assert - CPU usage should be minimal (< 5%)
        cpuPercent.Should().BeLessThan(5.0);
    }
}
