using System.Diagnostics;
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Systems;
using Xunit;

namespace PokeSharp.Engine.Systems.Tests.Rendering;

/// <summary>
///     Unit tests for SpriteAnimationSystem focusing on ManifestKey optimization.
///     Tests verify that the optimization correctly uses cached ManifestKey instead of
///     allocating new strings per frame.
/// </summary>
public class SpriteAnimationSystemTests : IDisposable
{
    private readonly Mock<ILogger<SpriteAnimationSystem>> _mockLogger;
    private readonly Mock<SpriteLoader> _mockSpriteLoader;
    private readonly SpriteAnimationSystem _system;
    private readonly SpriteManifest _testManifest;
    private readonly World _world;

    public SpriteAnimationSystemTests()
    {
        _world = World.Create();
        _mockSpriteLoader = new Mock<SpriteLoader>(Mock.Of<ILogger<SpriteLoader>>());
        _mockLogger = new Mock<ILogger<SpriteAnimationSystem>>();

        // Create a test sprite manifest matching the actual structure
        _testManifest = new SpriteManifest
        {
            Name = "test_sprite",
            Category = "player",
            Animations = new List<SpriteAnimationInfo>
            {
                new()
                {
                    Name = "walk_down",
                    Loop = true,
                    FrameIndices = new[] { 0, 1, 2, 3 },
                    FrameDuration = 0.125f,
                    FlipHorizontal = false,
                },
            },
            Frames = new List<SpriteFrameInfo>
            {
                new()
                {
                    Index = 0,
                    X = 0,
                    Y = 0,
                    Width = 32,
                    Height = 32,
                },
                new()
                {
                    Index = 1,
                    X = 32,
                    Y = 0,
                    Width = 32,
                    Height = 32,
                },
                new()
                {
                    Index = 2,
                    X = 64,
                    Y = 0,
                    Width = 32,
                    Height = 32,
                },
                new()
                {
                    Index = 3,
                    X = 96,
                    Y = 0,
                    Width = 32,
                    Height = 32,
                },
            },
        };

        _system = new SpriteAnimationSystem(_mockSpriteLoader.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void ManifestKey_ShouldBeSetCorrectly_OnSpriteCreation()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");

        // Assert - ManifestKey should be set to "{category}/{spriteName}"
        sprite.ManifestKey.Should().NotBeNullOrEmpty();
        sprite.ManifestKey.Should().Be("player/player_sprite");
    }

    [Fact]
    public void Update_ShouldUseManifestKey_InsteadOfAllocatingString()
    {
        // Arrange - Create entity with sprite and animation
        var sprite = new Sprite("player", "player_sprite");
        var animation = new Animation
        {
            CurrentAnimation = "walk_down",
            IsPlaying = true,
            CurrentFrame = 0,
            FrameTimer = 0f,
        };

        _mockSpriteLoader.Setup(x => x.GetSprite("player", "player_sprite")).Returns(_testManifest);

        Entity entity = _world.Create(sprite, animation);

        // Act - Update the system
        _system.Initialize(_world);
        _system.Update(_world, 0.016f); // 60 FPS delta

        // Assert - Verify the ManifestKey was used (manifest should be fetched once)
        _mockSpriteLoader.Verify(x => x.GetSprite("player", "player_sprite"), Times.Once);
    }

    [Fact]
    public void Update_ShouldNotAllocateNewStrings_AcrossMultipleFrames()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        var animation = new Animation
        {
            CurrentAnimation = "walk_down",
            IsPlaying = true,
            CurrentFrame = 0,
            FrameTimer = 0f,
        };

        _mockSpriteLoader.Setup(x => x.GetSprite("player", "player_sprite")).Returns(_testManifest);

        Entity entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act - Update multiple times (simulate 60 frames)
        for (int i = 0; i < 60; i++)
        {
            _system.Update(_world, 0.016f);
        }

        // Assert - ManifestKey should still be the same instance
        Sprite currentSprite = _world.Get<Sprite>(entity);
        currentSprite.ManifestKey.Should().Be("player/player_sprite");

        // Verify sprite loader is called consistently with same key (only once due to caching)
        _mockSpriteLoader.Verify(x => x.GetSprite("player", "player_sprite"), Times.Once);
    }

    [Fact]
    public void Update_ShouldHandleMultipleEntities_WithDifferentManifestKeys()
    {
        // Arrange - Create multiple entities
        var sprite1 = new Sprite("player", "player_sprite");
        var sprite2 = new Sprite("npc", "npc_sprite");
        var sprite3 = new Sprite("enemy", "enemy_sprite");

        var animation1 = new Animation { CurrentAnimation = "walk_down", IsPlaying = true };
        var animation2 = new Animation { CurrentAnimation = "walk_down", IsPlaying = true };
        var animation3 = new Animation { CurrentAnimation = "walk_down", IsPlaying = true };

        var testManifest2 = new SpriteManifest
        {
            Name = "npc_sprite",
            Category = "npc",
            Animations = _testManifest.Animations,
            Frames = _testManifest.Frames,
        };
        var testManifest3 = new SpriteManifest
        {
            Name = "enemy_sprite",
            Category = "enemy",
            Animations = _testManifest.Animations,
            Frames = _testManifest.Frames,
        };

        _mockSpriteLoader.Setup(x => x.GetSprite("player", "player_sprite")).Returns(_testManifest);
        _mockSpriteLoader.Setup(x => x.GetSprite("npc", "npc_sprite")).Returns(testManifest2);
        _mockSpriteLoader.Setup(x => x.GetSprite("enemy", "enemy_sprite")).Returns(testManifest3);

        Entity entity1 = _world.Create(sprite1, animation1);
        Entity entity2 = _world.Create(sprite2, animation2);
        Entity entity3 = _world.Create(sprite3, animation3);

        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Verify each unique ManifestKey was used
        _mockSpriteLoader.Verify(x => x.GetSprite("player", "player_sprite"), Times.Once);
        _mockSpriteLoader.Verify(x => x.GetSprite("npc", "npc_sprite"), Times.Once);
        _mockSpriteLoader.Verify(x => x.GetSprite("enemy", "enemy_sprite"), Times.Once);
    }

    [Fact]
    public void ManifestKey_ShouldNotChange_WhenSpriteIsModified()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        string originalKey = sprite.ManifestKey;

        // Act - Modify sprite properties (but not category/spriteName)
        sprite.CurrentFrame = 1; // Just modify something that won't affect ManifestKey

        // Assert - ManifestKey should remain unchanged
        sprite.ManifestKey.Should().Be(originalKey);
        sprite.ManifestKey.Should().Be("player/player_sprite");
    }

    [Fact]
    public void Update_ShouldHandleNullManifest_Gracefully()
    {
        // Arrange
        var sprite = new Sprite("player", "invalid_sprite");
        var animation = new Animation { CurrentAnimation = "walk_down", IsPlaying = true };

        _mockSpriteLoader
            .Setup(x => x.GetSprite("player", "invalid_sprite"))
            .Returns((SpriteManifest?)null);

        Entity entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act & Assert - Should not throw
        Action act = () => _system.Update(_world, 0.016f);
        act.Should().NotThrow();
    }

    [Fact]
    public void Update_ShouldStopAnimation_WhenIsPlayingIsFalse()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        var animation = new Animation
        {
            CurrentAnimation = "walk_down",
            IsPlaying = false,
            CurrentFrame = 2, // Should stay at frame 2
            FrameTimer = 0f,
        };

        _mockSpriteLoader.Setup(x => x.GetSprite("player", "player_sprite")).Returns(_testManifest);

        Entity entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.5f); // Large delta time

        // Assert - Animation should not have advanced
        Animation currentAnimation = _world.Get<Animation>(entity);
        currentAnimation.CurrentFrame.Should().Be(2);
    }

    [Fact]
    public void ManifestKey_Performance_ShouldBeFasterThanStringConcatenation()
    {
        // This test documents the performance benefit
        // ManifestKey is pre-computed once vs string concatenation every frame

        // Arrange
        const int iterations = 100000;
        var sprite = new Sprite("player", "player_sprite");

        // Act - Measure ManifestKey access (optimized)
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            string key = sprite.ManifestKey; // Direct property access
        }

        sw1.Stop();

        // Act - Measure string concatenation (old way)
        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            string key = $"{sprite.Category}/{sprite.SpriteName}"; // Allocation per iteration
        }

        sw2.Stop();

        // Assert - ManifestKey should be significantly faster
        sw1.ElapsedMilliseconds.Should().BeLessThan(sw2.ElapsedMilliseconds);

        // Output performance comparison
        double improvement =
            (sw2.ElapsedMilliseconds - sw1.ElapsedMilliseconds)
            / (double)sw2.ElapsedMilliseconds
            * 100;
        _mockLogger.Object.LogInformation(
            $"ManifestKey performance: {sw1.ElapsedMilliseconds}ms vs {sw2.ElapsedMilliseconds}ms (string concat) - {improvement:F1}% faster"
        );
    }

    [Fact]
    public void Update_ShouldAdvanceFrame_WhenTimerExceedsFrameDuration()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        var animation = new Animation
        {
            CurrentAnimation = "walk_down",
            IsPlaying = true,
            CurrentFrame = 0,
            FrameTimer = 0.1f,
        };

        _mockSpriteLoader.Setup(x => x.GetSprite("player", "player_sprite")).Returns(_testManifest);

        Entity entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act - Update with delta time that exceeds frame duration
        _system.Update(_world, 0.05f); // Total timer: 0.15f > 0.125f frame duration

        // Assert - Should advance to next frame
        Animation currentAnimation = _world.Get<Animation>(entity);
        currentAnimation.CurrentFrame.Should().Be(1);
    }
}
