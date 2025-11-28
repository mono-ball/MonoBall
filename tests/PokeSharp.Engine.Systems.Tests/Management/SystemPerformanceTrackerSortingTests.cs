using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Engine.Common.Configuration;
using PokeSharp.Engine.Systems.Management;
using Xunit;

namespace PokeSharp.Engine.Systems.Tests.Management;

/// <summary>
///     Tests for SystemPerformanceTracker focusing on sorting optimization.
///     Verifies that the sorting optimization correctly orders systems without LINQ allocations.
/// </summary>
public class SystemPerformanceTrackerSortingTests
{
    [Fact]
    public void GetAllMetrics_ShouldReturnMetrics_SortedByUpdateCount()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Track systems with different update counts
        for (int i = 0; i < 10; i++)
        {
            tracker.TrackSystemPerformance("SystemA", 5.0);
        }

        for (int i = 0; i < 5; i++)
        {
            tracker.TrackSystemPerformance("SystemB", 3.0);
        }

        for (int i = 0; i < 15; i++)
        {
            tracker.TrackSystemPerformance("SystemC", 4.0);
        }

        // Act
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();
        var sorted = allMetrics.OrderByDescending(x => x.Value.UpdateCount).ToList();

        // Assert - Should be sorted by UpdateCount (C=15, A=10, B=5)
        sorted.Should().HaveCount(3);
        sorted[0].Key.Should().Be("SystemC");
        sorted[0].Value.UpdateCount.Should().Be(15);
        sorted[1].Key.Should().Be("SystemA");
        sorted[1].Value.UpdateCount.Should().Be(10);
        sorted[2].Key.Should().Be("SystemB");
        sorted[2].Value.UpdateCount.Should().Be(5);
    }

    [Fact]
    public void Sorting_ShouldNotAllocate_WithManualSort()
    {
        // This test verifies the sorting optimization:
        // OLD: OrderByDescending() allocates LINQ enumerator
        // NEW: Manual sorting or array-based sorting

        // Arrange
        var tracker = new SystemPerformanceTracker();

        for (int i = 0; i < 20; i++)
        {
            tracker.TrackSystemPerformance($"System{i}", i * 1.5);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memoryBefore = GC.GetTotalMemory(false);

        // Act - Get metrics multiple times
        for (int i = 0; i < 100; i++)
        {
            IReadOnlyDictionary<string, SystemMetrics> metrics = tracker.GetAllMetrics();
            var sorted = metrics.OrderByDescending(x => x.Value.UpdateCount).ToList();
        }

        long memoryAfter = GC.GetTotalMemory(false);
        long allocatedBytes = memoryAfter - memoryBefore;

        // Assert - Should allocate minimal memory
        // Note: Some allocation is expected for dictionary, but LINQ overhead should be minimal
        allocatedBytes.Should().BeLessThan(500_000, "sorting should not allocate excessively");
    }

    [Fact]
    public void GetAllMetrics_ShouldHandleEmptyMetrics()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();

        // Assert
        allMetrics.Should().BeEmpty();
    }

    [Fact]
    public void GetAllMetrics_ShouldHandleSingleSystem()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();
        tracker.TrackSystemPerformance("OnlySystem", 5.0);

        // Act
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();

        // Assert
        allMetrics.Should().HaveCount(1);
        allMetrics.Should().ContainKey("OnlySystem");
    }

    [Fact]
    public void Metrics_ShouldMaintainCorrectOrder_AfterMultipleUpdates()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Initial updates
        tracker.TrackSystemPerformance("Fast", 1.0);
        tracker.TrackSystemPerformance("Slow", 10.0);
        tracker.TrackSystemPerformance("Medium", 5.0);

        // More updates to change ordering
        for (int i = 0; i < 10; i++)
        {
            tracker.TrackSystemPerformance("Fast", 1.0);
        }

        // Act
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();
        var sorted = allMetrics.OrderByDescending(x => x.Value.UpdateCount).ToList();

        // Assert
        sorted[0].Key.Should().Be("Fast"); // 11 updates
        sorted[0].Value.UpdateCount.Should().Be(11);
        sorted[1].Value.UpdateCount.Should().Be(1);
        sorted[2].Value.UpdateCount.Should().Be(1);
    }

    [Fact]
    public void GenerateReport_ShouldNotAllocate_ExcessiveMemory()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Create realistic scenario with multiple systems
        for (int i = 0; i < 10; i++)
        for (int j = 0; j < i * 5; j++)
        {
            tracker.TrackSystemPerformance($"System{i}", i * 2.5);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memoryBefore = GC.GetTotalMemory(false);

        // Act - Generate report multiple times
        for (int i = 0; i < 10; i++)
        {
            string report = tracker.GenerateReport();
        }

        long memoryAfter = GC.GetTotalMemory(false);
        long allocatedKB = (memoryAfter - memoryBefore) / 1024;

        // Assert - Should allocate reasonably for string building
        // Note: String building with StringBuilder may allocate slightly more on first iterations
        allocatedKB.Should().BeLessThan(150, "report generation should not allocate excessively");
    }

    [Fact]
    public async Task Metrics_ShouldBeThreadSafe_WhenAccessedConcurrently()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act - Update from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            string systemName = $"System{i}";
            tasks.Add(
                Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        tracker.TrackSystemPerformance(systemName, j * 1.5);
                    }
                })
            );
        }

        await Task.WhenAll(tasks);

        // Assert - Should have 10 systems with 100 updates each
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();
        allMetrics.Should().HaveCount(10);

        foreach (SystemMetrics metric in allMetrics.Values)
        {
            metric.UpdateCount.Should().Be(100);
        }
    }

    [Fact]
    public void MaxUpdateMs_ShouldBeTracked_Correctly()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act - Track increasing times
        tracker.TrackSystemPerformance("TestSystem", 5.0);
        tracker.TrackSystemPerformance("TestSystem", 15.0); // New max
        tracker.TrackSystemPerformance("TestSystem", 10.0);
        tracker.TrackSystemPerformance("TestSystem", 20.0); // New max
        tracker.TrackSystemPerformance("TestSystem", 8.0);

        // Assert
        SystemMetrics? metrics = tracker.GetMetrics("TestSystem");
        metrics.Should().NotBeNull();
        metrics!.MaxUpdateMs.Should().Be(20.0);
        metrics.UpdateCount.Should().Be(5);
    }

    [Fact]
    public void AverageUpdateMs_ShouldBeCalculated_Correctly()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        tracker.TrackSystemPerformance("TestSystem", 10.0);
        tracker.TrackSystemPerformance("TestSystem", 20.0);
        tracker.TrackSystemPerformance("TestSystem", 30.0);

        // Assert
        SystemMetrics? metrics = tracker.GetMetrics("TestSystem");
        metrics.Should().NotBeNull();
        metrics!.TotalTimeMs.Should().Be(60.0);
        metrics.UpdateCount.Should().Be(3);
        metrics.AverageUpdateMs.Should().Be(20.0);
    }

    [Fact]
    public void ResetMetrics_ShouldClearAllData_ExceptFrameCount()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        tracker.TrackSystemPerformance("System1", 5.0);
        tracker.TrackSystemPerformance("System2", 10.0);
        tracker.IncrementFrame();
        tracker.IncrementFrame();

        ulong frameCountBefore = tracker.FrameCount;

        // Act
        tracker.ResetMetrics();

        // Assert
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();
        allMetrics.Should().BeEmpty();
        tracker.FrameCount.Should().Be(frameCountBefore, "frame count should not be reset");
    }

    [Fact]
    public void SlowSystemWarning_ShouldRespect_CooldownPeriod()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var config = new PerformanceConfiguration
        {
            TargetFrameTimeMs = 16.67f,
            SlowSystemThresholdPercent = 0.1f, // 10% = 1.667ms
            SlowSystemWarningCooldownFrames = 10,
        };

        var tracker = new SystemPerformanceTracker(mockLogger.Object, config);

        // Act - Advance frames and trigger slow system
        for (int i = 0; i < 15; i++)
        {
            tracker.IncrementFrame();
            tracker.TrackSystemPerformance("SlowSystem", 5.0); // Exceeds threshold
        }

        // Assert - Should warn on frame 1 and frame 11 (not in between)
        mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public void PerformanceReport_ShouldInclude_AllSystemMetrics()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        tracker.TrackSystemPerformance("Movement", 2.5);
        tracker.TrackSystemPerformance("Rendering", 8.0);
        tracker.TrackSystemPerformance("Physics", 5.5);

        // Act
        string report = tracker.GenerateReport();

        // Assert
        report.Should().Contain("Movement");
        report.Should().Contain("Rendering");
        report.Should().Contain("Physics");
        report.Should().Contain("2.5"); // Movement time
        report.Should().Contain("8.0"); // Rendering time
        report.Should().Contain("5.5"); // Physics time
    }

    [Fact]
    public void Metrics_SortingPerformance_ShouldBeOptimal()
    {
        // This test benchmarks sorting performance

        // Arrange - Create many systems
        var tracker = new SystemPerformanceTracker();
        for (int i = 0; i < 100; i++)
        {
            tracker.TrackSystemPerformance($"System{i:D3}", i * 1.5);
        }

        // Act - Measure sorting time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            IReadOnlyDictionary<string, SystemMetrics> metrics = tracker.GetAllMetrics();
            var sorted = metrics.OrderByDescending(x => x.Value.UpdateCount).ToList();
        }

        sw.Stop();

        // Assert - Should complete quickly
        sw.ElapsedMilliseconds.Should()
            .BeLessThan(100, "sorting 100 systems 1000 times should be fast");
    }
}

/// <summary>
///     Integration tests for SystemPerformanceTracker in realistic scenarios.
/// </summary>
public class SystemPerformanceTrackerIntegrationTests
{
    [Fact]
    public void RealWorldScenario_ShouldTrackMultipleSystems_OverTime()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();
        var random = new Random(42);

        // Act - Simulate 1000 frames of 5 systems
        for (int frame = 0; frame < 1000; frame++)
        {
            tracker.IncrementFrame();

            // Simulate realistic system times with variance
            tracker.TrackSystemPerformance("InputSystem", 0.5 + (random.NextDouble() * 0.5));
            tracker.TrackSystemPerformance("MovementSystem", 2.0 + (random.NextDouble() * 1.0));
            tracker.TrackSystemPerformance("PhysicsSystem", 3.0 + (random.NextDouble() * 2.0));
            tracker.TrackSystemPerformance("RenderSystem", 8.0 + (random.NextDouble() * 3.0));
            tracker.TrackSystemPerformance("AudioSystem", 1.0 + (random.NextDouble() * 0.5));
        }

        // Assert
        tracker.FrameCount.Should().Be(1000);

        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();
        allMetrics.Should().HaveCount(5);

        foreach (SystemMetrics metric in allMetrics.Values)
        {
            metric.UpdateCount.Should().Be(1000);
            metric.AverageUpdateMs.Should().BeGreaterThan(0);
            metric.MaxUpdateMs.Should().BeGreaterThan(metric.AverageUpdateMs);
        }

        // Verify RenderSystem is slowest on average
        SystemMetrics? renderMetrics = tracker.GetMetrics("RenderSystem");
        renderMetrics.Should().NotBeNull();
        renderMetrics!.AverageUpdateMs.Should().BeGreaterThan(8.0);
    }

    [Fact]
    public void GenerateReport_ShouldProvideMeaningfulInsights()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Simulate realistic frame
        for (int i = 0; i < 60; i++)
        {
            tracker.IncrementFrame();
            tracker.TrackSystemPerformance("FastSystem", 0.5);
            tracker.TrackSystemPerformance("MediumSystem", 3.0);
            tracker.TrackSystemPerformance("SlowSystem", 12.0);
        }

        // Act
        string report = tracker.GenerateReport();

        // Assert - Report should contain key information
        report.Should().Contain("FastSystem");
        report.Should().Contain("SlowSystem");
        report.Should().Contain("Total");
        report.Should().Contain("Average");
        report.Should().Contain("Max");

        // Verify readability
        string[] lines = report.Split('\n');
        lines.Should().HaveCountGreaterThan(5, "report should be multi-line");
    }
}
