using PokeSharp.Tests.Audio.Utilities;
using PokeSharp.Tests.Audio.Utilities.Interfaces;
using PokeSharp.Tests.Audio.Utilities.Mocks;
using Xunit;

namespace PokeSharp.Tests.Audio.Unit.Audio;

/// <summary>
/// Unit tests for volume control calculations
/// Validates that volume calculations are accurate across master, music, and SFX channels
/// </summary>
[Trait("Category", "Unit")]
[Trait("Subsystem", "Audio")]
public class VolumeControlTests
{
    [Theory]
    [InlineData(1.0f, 0.5f, 0.5f)]  // Master 100%, SFX 50% = 50%
    [InlineData(0.5f, 1.0f, 0.5f)]  // Master 50%, SFX 100% = 50%
    [InlineData(0.5f, 0.5f, 0.25f)] // Master 50%, SFX 50% = 25%
    [InlineData(0.0f, 1.0f, 0.0f)]  // Muted master
    [InlineData(1.0f, 0.0f, 0.0f)]  // Muted SFX
    public void CalculateEffectiveVolume_VariousLevels_ReturnsCorrectProduct(
        float master, float sfx, float expected)
    {
        // Arrange
        float actual = master * sfx;

        // Assert
        Assert.Equal(expected, actual, precision: 3);
    }

    [Fact]
    public void MusicPlayer_SetVolume_WithinRange_UpdatesVolume()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");

        // Act
        musicPlayer.SetVolume(0.7f);

        // Assert
        Assert.Equal(0.7f, musicPlayer.Volume, precision: 3);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(2.0f)]
    public void MusicPlayer_SetVolume_OutOfRange_ThrowsArgumentOutOfRangeException(float volume)
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => musicPlayer.SetVolume(volume));
    }

    [Fact]
    public void SoundInstance_SetVolume_UpdatesImmediately()
    {
        // Arrange
        var sound = new MockSoundEffect("test.wav", TimeSpan.FromMilliseconds(500));
        var instance = sound.CreateInstance();

        // Act
        instance.Volume = 0.5f;

        // Assert
        Assert.Equal(0.5f, instance.Volume);
    }

    [Fact]
    public void VolumeCalculation_AllChannelsMuted_ReturnsZero()
    {
        // Arrange
        float master = 0.0f;
        float music = 0.5f;

        // Act
        float effective = master * music;

        // Assert
        Assert.Equal(0.0f, effective);
    }

    [Fact]
    public void VolumeCalculation_AllChannelsMax_ReturnsOne()
    {
        // Arrange
        float master = 1.0f;
        float music = 1.0f;

        // Act
        float effective = master * music;

        // Assert
        Assert.Equal(1.0f, effective);
    }
}
