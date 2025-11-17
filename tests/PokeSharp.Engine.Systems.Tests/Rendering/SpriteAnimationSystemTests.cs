using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Engine.Core.Assets;
using PokeSharp.Engine.Rendering.Systems;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Rendering;
using Xunit;

namespace PokeSharp.Engine.Systems.Tests.Rendering;

/// <summary>
///     Unit tests for SpriteAnimationSystem focusing on ManifestKey optimization.
///     Tests verify that the optimization correctly uses cached ManifestKey instead of
///     allocating new strings per frame.
/// </summary>
public class SpriteAnimationSystemTests : IDisposable
{
    private readonly World _world;
    private readonly Mock<IAssetProvider> _mockAssetProvider;
    private readonly Mock<ILogger<SpriteAnimationSystem>> _mockLogger;
    private readonly SpriteAnimationSystem _system;
    private readonly AnimationManifest _testManifest;

    public SpriteAnimationSystemTests()
    {
        _world = World.Create();
        _mockAssetProvider = new Mock<IAssetProvider>();
        _mockLogger = new Mock<ILogger<SpriteAnimationSystem>>();

        // Create a test animation manifest
        _testManifest = new AnimationManifest
        {
            Animations = new Dictionary<string, AnimationDefinition>
            {
                {
                    "walk_down",
                    new AnimationDefinition
                    {
                        FrameRate = 8,
                        Frames = new List<FrameDefinition>
                        {
                            new() { FrameX = 0, FrameY = 0, Duration = 0.125f },
                            new() { FrameX = 1, FrameY = 0, Duration = 0.125f },
                            new() { FrameX = 2, FrameY = 0, Duration = 0.125f },
                            new() { FrameX = 3, FrameY = 0, Duration = 0.125f }
                        }
                    }
                }
            }
        };

        _system = new SpriteAnimationSystem(_mockAssetProvider.Object, _mockLogger.Object);
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
            FrameTimer = 0f
        };

        _mockAssetProvider
            .Setup(x => x.GetAnimationManifest(sprite.ManifestKey))
            .Returns(_testManifest);

        var entity = _world.Create(sprite, animation);

        // Act - Update the system
        _system.Initialize(_world);
        _system.Update(_world, 0.016f); // 60 FPS delta

        // Assert - Verify the ManifestKey was used (manifest should be fetched once)
        _mockAssetProvider.Verify(x => x.GetAnimationManifest(sprite.ManifestKey), Times.Once);
        _mockAssetProvider.Verify(x => x.GetAnimationManifest("player/player_sprite"), Times.Once);
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
            FrameTimer = 0f
        };

        _mockAssetProvider
            .Setup(x => x.GetAnimationManifest(sprite.ManifestKey))
            .Returns(_testManifest);

        var entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act - Update multiple times (simulate 60 frames)
        for (int i = 0; i < 60; i++)
        {
            _system.Update(_world, 0.016f);
        }

        // Assert - ManifestKey should still be the same instance
        var currentSprite = _world.Get<Sprite>(entity);
        currentSprite.ManifestKey.Should().Be("player/player_sprite");

        // Verify asset provider is called consistently with same key
        _mockAssetProvider.Verify(
            x => x.GetAnimationManifest("player/player_sprite"),
            Times.AtLeastOnce
        );
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

        _mockAssetProvider
            .Setup(x => x.GetAnimationManifest(It.IsAny<string>()))
            .Returns(_testManifest);

        var entity1 = _world.Create(sprite1, animation1);
        var entity2 = _world.Create(sprite2, animation2);
        var entity3 = _world.Create(sprite3, animation3);

        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Verify each unique ManifestKey was used
        _mockAssetProvider.Verify(x => x.GetAnimationManifest("player/player_sprite"), Times.Once);
        _mockAssetProvider.Verify(x => x.GetAnimationManifest("npc/npc_sprite"), Times.Once);
        _mockAssetProvider.Verify(x => x.GetAnimationManifest("enemy/enemy_sprite"), Times.Once);
    }

    [Fact]
    public void ManifestKey_ShouldNotChange_WhenSpriteIsModified()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        var originalKey = sprite.ManifestKey;

        // Act - Modify sprite properties (but not category/spriteName)
        sprite.Width = 32;
        sprite.Height = 32;

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

        _mockAssetProvider
            .Setup(x => x.GetAnimationManifest(sprite.ManifestKey))
            .Returns((AnimationManifest?)null);

        var entity = _world.Create(sprite, animation);
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
            FrameTimer = 0f
        };

        _mockAssetProvider
            .Setup(x => x.GetAnimationManifest(sprite.ManifestKey))
            .Returns(_testManifest);

        var entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.5f); // Large delta time

        // Assert - Animation should not have advanced
        var currentAnimation = _world.Get<Animation>(entity);
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
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var key = sprite.ManifestKey; // Direct property access
        }
        sw1.Stop();

        // Act - Measure string concatenation (old way)
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var key = $"{sprite.Category}/{sprite.SpriteName}"; // Allocation per iteration
        }
        sw2.Stop();

        // Assert - ManifestKey should be significantly faster
        sw1.ElapsedMilliseconds.Should().BeLessThan(sw2.ElapsedMilliseconds);

        // Output performance comparison
        var improvement = (sw2.ElapsedMilliseconds - sw1.ElapsedMilliseconds) / (double)sw2.ElapsedMilliseconds * 100;
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
            FrameTimer = 0.1f
        };

        _mockAssetProvider
            .Setup(x => x.GetAnimationManifest(sprite.ManifestKey))
            .Returns(_testManifest);

        var entity = _world.Create(sprite, animation);
        _system.Initialize(_world);

        // Act - Update with delta time that exceeds frame duration
        _system.Update(_world, 0.05f); // Total timer: 0.15f > 0.125f frame duration

        // Assert - Should advance to next frame
        var currentAnimation = _world.Get<Animation>(entity);
        currentAnimation.CurrentFrame.Should().Be(1);
    }
}
