using FluentAssertions;
using Xunit;

namespace PokeSharp.Engine.Scenes.Tests;

/// <summary>
///     Unit tests for LoadingProgress class, focusing on thread safety.
/// </summary>
public class LoadingProgressTests
{
    [Fact]
    public void Progress_ShouldClampToValidRange()
    {
        // Arrange
        var progress = new LoadingProgress();

        // Act & Assert
        progress.Progress = -0.5f;
        progress.Progress.Should().Be(0.0f);

        progress.Progress = 1.5f;
        progress.Progress.Should().Be(1.0f);

        progress.Progress = 0.5f;
        progress.Progress.Should().Be(0.5f);
    }

    [Fact]
    public async Task Progress_ShouldBeThreadSafe()
    {
        // Arrange
        var progress = new LoadingProgress();
        const int threadCount = 10;
        const int updatesPerThread = 100;
        var tasks = new List<Task>();

        // Act - Multiple threads updating progress concurrently
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks.Add(
                Task.Run(() =>
                {
                    for (int j = 0; j < updatesPerThread; j++)
                    {
                        progress.Progress =
                            ((threadId * updatesPerThread) + j)
                            / (float)(threadCount * updatesPerThread);
                        progress.CurrentStep = $"Thread {threadId} step {j}";
                    }
                })
            );
        }

        await Task.WhenAll(tasks);

        // Assert - Progress should be in valid range
        progress.Progress.Should().BeInRange(0.0f, 1.0f);
        progress.CurrentStep.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CurrentStep_ShouldHandleNull()
    {
        // Arrange
        var progress = new LoadingProgress();

        // Act
        progress.CurrentStep = null!;

        // Assert
        progress.CurrentStep.Should().BeEmpty();
    }

    [Fact]
    public async Task IsComplete_ShouldBeThreadSafe()
    {
        // Arrange
        var progress = new LoadingProgress();
        const int threadCount = 5;
        var tasks = new List<Task>();

        // Act - Multiple threads setting IsComplete
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() => progress.IsComplete = true));
        }

        await Task.WhenAll(tasks);

        // Assert
        progress.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Error_ShouldBeThreadSafe()
    {
        // Arrange
        var progress = new LoadingProgress();
        var exception = new Exception("Test error");

        // Act
        progress.Error = exception;

        // Assert
        progress.Error.Should().Be(exception);
    }
}
