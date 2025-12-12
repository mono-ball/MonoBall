using PokeSharp.Tests.Audio.Utilities;
using PokeSharp.Tests.Audio.Utilities.Interfaces;
using Xunit;

namespace PokeSharp.Tests.Audio.Unit.Audio;

/// <summary>
/// Unit tests for audio state machine transitions
/// Validates state transitions for music and sound effects
/// </summary>
[Trait("Category", "Unit")]
[Trait("Subsystem", "Audio")]
public class AudioStateTransitionTests
{
    [Fact]
    public void PlayMusic_FromStopped_TransitionsToPlaying()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        Assert.Equal(MusicState.Stopped, musicPlayer.State);

        // Act
        musicPlayer.Play("bgm/route_1.ogg");

        // Assert
        Assert.Equal(MusicState.Playing, musicPlayer.State);
    }

    [Fact]
    public void PauseMusic_WhilePlaying_TransitionsToPaused()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/battle.ogg");

        // Act
        musicPlayer.Pause();

        // Assert
        Assert.Equal(MusicState.Paused, musicPlayer.State);
    }

    [Fact]
    public void ResumeMusic_WhilePaused_TransitionsToPlaying()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/title.ogg");
        musicPlayer.Pause();

        // Act
        musicPlayer.Resume();

        // Assert
        Assert.Equal(MusicState.Playing, musicPlayer.State);
    }

    [Fact]
    public void CrossFade_DuringPlayback_TransitionsToFadingOut()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/town.ogg");

        // Act
        musicPlayer.CrossFade("bgm/route.ogg", TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(MusicState.FadingOut, musicPlayer.State);
    }

    [Fact]
    public void StopMusic_FromAnyState_TransitionsToStopped()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");
        musicPlayer.Pause();

        // Act
        musicPlayer.Stop();

        // Assert
        Assert.Equal(MusicState.Stopped, musicPlayer.State);
    }

    [Theory]
    [InlineData(MusicState.Stopped)]
    public void Pause_FromInvalidState_ThrowsInvalidOperationException(MusicState targetState)
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();

        // Ensure we're in Stopped state
        if (targetState == MusicState.Stopped)
        {
            musicPlayer.Stop();
        }

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => musicPlayer.Pause());
    }

    [Fact]
    public void Resume_FromStopped_ThrowsInvalidOperationException()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => musicPlayer.Resume());
    }

    [Fact]
    public void FadeOut_WhilePlaying_TransitionsToFadingOut()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");

        // Act
        musicPlayer.FadeOut(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(MusicState.FadingOut, musicPlayer.State);
    }

    [Fact]
    public void FadeIn_WhilePlaying_TransitionsToFadingIn()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");
        musicPlayer.SetVolume(0.5f);

        // Act
        musicPlayer.FadeIn(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(MusicState.FadingIn, musicPlayer.State);
    }

    [Fact]
    public void PlayWithFadeIn_InitiallyFadingIn()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();

        // Act
        musicPlayer.Play("bgm/test.ogg", fadeIn: TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(MusicState.FadingIn, musicPlayer.State);
        Assert.Equal(0f, musicPlayer.Volume);
    }

    [Fact]
    public void CancelFade_WhileFading_TransitionsToPlaying()
    {
        // Arrange
        var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");
        musicPlayer.FadeOut(TimeSpan.FromSeconds(2));

        // Act
        musicPlayer.CancelFade();

        // Assert
        Assert.Equal(MusicState.Playing, musicPlayer.State);
    }

    [Fact]
    public void SoundInstance_Play_SetsIsPlayingTrue()
    {
        // Arrange
        var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
        var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
        var instance = sound.CreateInstance();

        // Act
        instance.Play();

        // Assert
        Assert.True(instance.IsPlaying);
        Assert.False(instance.IsPaused);
    }

    [Fact]
    public void SoundInstance_Stop_SetsIsPlayingFalse()
    {
        // Arrange
        var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
        var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
        var instance = sound.CreateInstance();
        instance.Play();

        // Act
        instance.Stop();

        // Assert
        Assert.False(instance.IsPlaying);
        Assert.False(instance.IsPaused);
    }

    [Fact]
    public void SoundInstance_Pause_SetsIsPausedTrue()
    {
        // Arrange
        var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
        var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
        var instance = sound.CreateInstance();
        instance.Play();

        // Act
        instance.Pause();

        // Assert
        Assert.True(instance.IsPaused);
    }

    [Fact]
    public void SoundInstance_Resume_ClearsIsPaused()
    {
        // Arrange
        var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
        var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
        var instance = sound.CreateInstance();
        instance.Play();
        instance.Pause();

        // Act
        instance.Resume();

        // Assert
        Assert.True(instance.IsPlaying);
        Assert.False(instance.IsPaused);
    }
}
