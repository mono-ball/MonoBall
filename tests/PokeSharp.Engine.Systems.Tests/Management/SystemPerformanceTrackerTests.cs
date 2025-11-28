using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Engine.Common.Configuration;
using PokeSharp.Engine.Systems.Management;
using Xunit;

namespace PokeSharp.Engine.Systems.Tests.Management;

/// <summary>
///     Unit tests for SystemPerformanceTracker.
/// </summary>
public class SystemPerformanceTrackerTests
{
    [Fact]
    public void TrackSystemPerformance_RecordsMetrics()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        tracker.TrackSystemPerformance("TestSystem", 5.0);
        tracker.TrackSystemPerformance("TestSystem", 10.0);
        tracker.TrackSystemPerformance("TestSystem", 7.5);

        // Assert
        SystemMetrics? metrics = tracker.GetMetrics("TestSystem");
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics.UpdateCount);
        Assert.Equal(22.5, metrics.TotalTimeMs);
        Assert.Equal(7.5, metrics.AverageUpdateMs);
        Assert.Equal(10.0, metrics.MaxUpdateMs);
        Assert.Equal(7.5, metrics.LastUpdateMs);
    }

    [Fact]
    public void TrackSystemPerformance_UpdatesMaxTime()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        tracker.TrackSystemPerformance("TestSystem", 5.0);
        tracker.TrackSystemPerformance("TestSystem", 15.0); // New max
        tracker.TrackSystemPerformance("TestSystem", 7.0);

        // Assert
        SystemMetrics? metrics = tracker.GetMetrics("TestSystem");
        Assert.NotNull(metrics);
        Assert.Equal(15.0, metrics.MaxUpdateMs);
    }

    [Fact]
    public void IncrementFrame_IncrementsCounter()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        tracker.IncrementFrame();
        tracker.IncrementFrame();
        tracker.IncrementFrame();

        // Assert
        Assert.Equal(3UL, tracker.FrameCount);
    }

    [Fact]
    public void GetMetrics_ReturnsNullForUnknownSystem()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        SystemMetrics? metrics = tracker.GetMetrics("NonExistentSystem");

        // Assert
        Assert.Null(metrics);
    }

    [Fact]
    public void GetAllMetrics_ReturnsAllTrackedSystems()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act
        tracker.TrackSystemPerformance("System1", 5.0);
        tracker.TrackSystemPerformance("System2", 10.0);
        tracker.TrackSystemPerformance("System3", 15.0);

        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();

        // Assert
        Assert.Equal(3, allMetrics.Count);
        Assert.Contains("System1", allMetrics.Keys);
        Assert.Contains("System2", allMetrics.Keys);
        Assert.Contains("System3", allMetrics.Keys);
    }

    [Fact]
    public void ResetMetrics_ClearsMetricsData()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();
        tracker.TrackSystemPerformance("System1", 5.0);
        tracker.TrackSystemPerformance("System2", 10.0);
        tracker.IncrementFrame();
        tracker.IncrementFrame();

        // Act
        tracker.ResetMetrics();

        // Assert
        IReadOnlyDictionary<string, SystemMetrics> allMetrics = tracker.GetAllMetrics();
        Assert.Empty(allMetrics);
        // Note: Frame counter is not reset by ResetMetrics
        Assert.Equal(2UL, tracker.FrameCount);
    }

    [Fact]
    public void TrackSystemPerformance_LogsSlowSystemWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        // Set up IsEnabled to return true for Warning level
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

        var config = new PerformanceConfiguration
        {
            TargetFrameTimeMs = 16.67f,
            SlowSystemThresholdPercent = 0.1, // 10% = 1.667ms
            SlowSystemWarningCooldownFrames = 10,
        };
        var tracker = new SystemPerformanceTracker(mockLogger.Object, config);

        // Act - Advance frame counter to pass cooldown check (needs >= 10 frames)
        for (int i = 0; i < 10; i++)
        {
            tracker.IncrementFrame();
        }

        // Execute a slow system (3ms > 1.667ms threshold)
        tracker.TrackSystemPerformance("SlowSystem", 3.0);

        // Assert - Verify warning was logged
        mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SlowSystem")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void TrackSystemPerformance_ThrottlesSlowSystemWarnings()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var config = new PerformanceConfiguration
        {
            TargetFrameTimeMs = 16.67f,
            SlowSystemThresholdPercent = 0.1,
            SlowSystemWarningCooldownFrames = 5, // Warn every 5 frames
        };
        var tracker = new SystemPerformanceTracker(mockLogger.Object, config);

        // Act - Execute slow system 10 times with frame increments
        for (int i = 0; i < 10; i++)
        {
            tracker.IncrementFrame();
            tracker.TrackSystemPerformance("SlowSystem", 3.0);
        }

        // Assert - Should only warn twice (frame 1 and frame 6+)
        mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SlowSystem")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public void TrackSystemPerformance_ThrowsOnNullSystemName()
    {
        // Arrange
        var tracker = new SystemPerformanceTracker();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tracker.TrackSystemPerformance(null!, 5.0));
    }

    [Fact]
    public void Constructor_AcceptsNullLogger()
    {
        // Act & Assert - Should not throw
        var tracker = new SystemPerformanceTracker();
        tracker.TrackSystemPerformance("TestSystem", 5.0);
    }

    [Fact]
    public void Constructor_UsesDefaultConfigWhenNull()
    {
        // Arrange & Act
        var tracker = new SystemPerformanceTracker();
        tracker.TrackSystemPerformance("TestSystem", 5.0);

        // Assert - Should use default configuration without errors
        SystemMetrics? metrics = tracker.GetMetrics("TestSystem");
        Assert.NotNull(metrics);
    }
}
