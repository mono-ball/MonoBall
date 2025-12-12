using PokeSharp.Tests.Audio.Utilities.Interfaces;
using PokeSharp.Tests.Audio.Utilities.Mocks;

namespace PokeSharp.Tests.Audio.Utilities;

/// <summary>
/// Factory for creating configured test instances
/// </summary>
public static class AudioTestFactory
{
    /// <summary>
    /// Creates a mock audio content manager with default test assets
    /// </summary>
    public static MockAudioContentManager CreateMockContentManager()
    {
        return new MockAudioContentManager();
    }

    /// <summary>
    /// Creates a mock content manager with commonly used test assets pre-registered
    /// </summary>
    public static MockAudioContentManager CreateMockContentManagerWithDefaults()
    {
        var contentManager = new MockAudioContentManager();
        RegisterDefaultAssets(contentManager);
        return contentManager;
    }

    /// <summary>
    /// Registers default test assets to a content manager
    /// </summary>
    public static void RegisterDefaultAssets(MockAudioContentManager contentManager)
    {
        // Common test sounds
        contentManager.RegisterSound("sfx/test.wav", TimeSpan.FromMilliseconds(500));
        contentManager.RegisterSound("sfx/menu_select.wav", TimeSpan.FromMilliseconds(100));
        contentManager.RegisterSound("sfx/jump.wav", TimeSpan.FromMilliseconds(300));
        contentManager.RegisterSound("sfx/land.wav", TimeSpan.FromMilliseconds(250));
        contentManager.RegisterSound("sfx/walking.wav", TimeSpan.FromMilliseconds(400));
        contentManager.RegisterSound("sfx/ambient.wav", TimeSpan.FromMilliseconds(2000));

        // Battle sounds
        contentManager.RegisterSound("sfx/battle/hit.wav", TimeSpan.FromMilliseconds(200));
        contentManager.RegisterSound("sfx/battle/miss.wav", TimeSpan.FromMilliseconds(150));
        contentManager.RegisterSound("sfx/battle/critical.wav", TimeSpan.FromMilliseconds(400));
        contentManager.RegisterSound("sfx/battle/faint.wav", TimeSpan.FromMilliseconds(800));
        contentManager.RegisterSound("sfx/battle/pokemon_appear.wav", TimeSpan.FromMilliseconds(600));
        contentManager.RegisterSound("sfx/battle/victory.wav", TimeSpan.FromMilliseconds(3000));
        contentManager.RegisterSound("sfx/battle_intro.wav", TimeSpan.FromMilliseconds(1000));

        // Pokemon cries
        contentManager.RegisterSound("cries/025.wav", TimeSpan.FromMilliseconds(600)); // Pikachu
        contentManager.RegisterSound("cries/006.wav", TimeSpan.FromMilliseconds(800)); // Charizard
        contentManager.RegisterSound("cries/150.wav", TimeSpan.FromMilliseconds(900)); // Mewtwo
        contentManager.RegisterSound("cries/001.wav", TimeSpan.FromMilliseconds(500)); // Bulbasaur
        contentManager.RegisterSound("cries/004.wav", TimeSpan.FromMilliseconds(550)); // Charmander

        // Menu sounds
        contentManager.RegisterSound("sfx/menu_move.wav", TimeSpan.FromMilliseconds(50));
        contentManager.RegisterSound("sfx/menu_open.wav", TimeSpan.FromMilliseconds(200));
        contentManager.RegisterSound("sfx/menu_close.wav", TimeSpan.FromMilliseconds(150));

        // Terrain sounds
        contentManager.RegisterSound("sfx/ice_slide.wav", TimeSpan.FromMilliseconds(400));
        contentManager.RegisterSound("sfx/conveyor.wav", TimeSpan.FromMilliseconds(350));

        // Music tracks
        contentManager.RegisterMusic("bgm/route_1.ogg", TimeSpan.FromSeconds(120));
        contentManager.RegisterMusic("bgm/battle_wild.ogg", TimeSpan.FromSeconds(90));
        contentManager.RegisterMusic("bgm/battle_trainer.ogg", TimeSpan.FromSeconds(100));
        contentManager.RegisterMusic("bgm/town.ogg", TimeSpan.FromSeconds(150));
        contentManager.RegisterMusic("bgm/victory.ogg", TimeSpan.FromSeconds(10));
        contentManager.RegisterMusic("bgm/title.ogg", TimeSpan.FromSeconds(180));
        contentManager.RegisterMusic("bgm/route.ogg", TimeSpan.FromSeconds(130));
        contentManager.RegisterMusic("bgm/test.ogg", TimeSpan.FromSeconds(30));
        contentManager.RegisterMusic("bgm/short_loop.ogg", TimeSpan.FromSeconds(5));
        contentManager.RegisterMusic("bgm/intro_loop.ogg", TimeSpan.FromSeconds(40));
        contentManager.RegisterMusic("bgm/short_jingle.ogg", TimeSpan.FromSeconds(3), streaming: false);
        contentManager.RegisterMusic("bgm/long_track.ogg", TimeSpan.FromMinutes(3), streaming: true);

        // Additional test sounds (for stress testing)
        for (int i = 0; i < 100; i++)
        {
            contentManager.RegisterSound($"sfx/test_{i}.wav", TimeSpan.FromMilliseconds(100 + i * 10));
        }

        // Ambient sounds
        for (int i = 0; i < 10; i++)
        {
            contentManager.RegisterSound($"sfx/ambient_{i}.wav", TimeSpan.FromMilliseconds(500 + i * 50));
        }

        // Hit sounds for priority testing
        for (int i = 1; i <= 5; i++)
        {
            contentManager.RegisterSound($"sfx/hit{i}.wav", TimeSpan.FromMilliseconds(150));
        }

        // Low priority sounds
        for (int i = 0; i < 10; i++)
        {
            contentManager.RegisterSound($"sfx/low_{i}.wav", TimeSpan.FromMilliseconds(200));
        }

        // High priority sounds
        for (int i = 0; i < 5; i++)
        {
            contentManager.RegisterSound($"sfx/high_{i}.wav", TimeSpan.FromMilliseconds(300));
        }

        // Normal priority sounds
        for (int i = 0; i < 10; i++)
        {
            contentManager.RegisterSound($"sfx/normal.wav", TimeSpan.FromMilliseconds(250));
        }

        // Critical sound
        contentManager.RegisterSound("sfx/critical.wav", TimeSpan.FromMilliseconds(400));

        // Important sound
        contentManager.RegisterSound("sfx/important.wav", TimeSpan.FromMilliseconds(350));

        // Low priority sound
        contentManager.RegisterSound("sfx/low.wav", TimeSpan.FromMilliseconds(200));

        // Multiple tracks for crossfade testing
        contentManager.RegisterMusic("bgm/track1.ogg", TimeSpan.FromSeconds(60));
        contentManager.RegisterMusic("bgm/track2.ogg", TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Creates a mock music player
    /// </summary>
    public static MockMusicPlayer CreateMockMusicPlayer()
    {
        return new MockMusicPlayer();
    }

    /// <summary>
    /// Creates an audio event recorder
    /// </summary>
    public static AudioEventRecorder CreateEventRecorder()
    {
        return new AudioEventRecorder();
    }

    /// <summary>
    /// Creates a performance profiler
    /// </summary>
    public static AudioPerformanceProfiler CreatePerformanceProfiler()
    {
        return new AudioPerformanceProfiler();
    }
}
