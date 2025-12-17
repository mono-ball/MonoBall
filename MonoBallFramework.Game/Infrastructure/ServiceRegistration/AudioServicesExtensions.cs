using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Services;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.GameData;

namespace MonoBallFramework.Game.Infrastructure.ServiceRegistration;

/// <summary>
///     Extension methods for registering audio services.
/// </summary>
public static class AudioServicesExtensions
{
    /// <summary>
    ///     Registers PortAudio-based audio services with the DI container.
    ///     Cross-platform audio implementation using PortAudioSharp2 and NVorbis.
    /// </summary>
    public static IServiceCollection AddAudioServices(
        this IServiceCollection services,
        AudioConfiguration? config = null)
    {
        // Register configuration
        services.AddSingleton(config ?? AudioConfiguration.CreateDefault());

        // Register AudioRegistry (queries from EF Core GameDataContext using factory pattern)
        services.AddSingleton<AudioRegistry>(sp =>
        {
            IDbContextFactory<GameDataContext> contextFactory =
                sp.GetRequiredService<IDbContextFactory<GameDataContext>>();
            ILogger<AudioRegistry> logger = sp.GetRequiredService<ILogger<AudioRegistry>>();
            var registry = new AudioRegistry(contextFactory, logger);

            // Load audio definitions into cache for fast runtime access
            registry.LoadDefinitions();

            return registry;
        });

        // Register PortAudio-based sound effect manager
        services.AddSingleton<ISoundEffectManager>(sp =>
        {
            AudioRegistry audioRegistry = sp.GetRequiredService<AudioRegistry>();
            IContentProvider contentProvider = sp.GetRequiredService<IContentProvider>();
            AudioConfiguration audioConfig = sp.GetRequiredService<AudioConfiguration>();
            ILogger<PortAudioSoundEffectManager>? logger = sp.GetService<ILogger<PortAudioSoundEffectManager>>();

            return new PortAudioSoundEffectManager(
                audioRegistry,
                contentProvider,
                audioConfig.MaxConcurrentSounds,
                logger);
        });

        // Register PortAudio-based streaming music player
        // Streams audio on-demand (~64KB per stream vs ~32MB per cached track)
        services.AddSingleton<IMusicPlayer>(sp =>
        {
            AudioRegistry audioRegistry = sp.GetRequiredService<AudioRegistry>();
            IContentProvider contentProvider = sp.GetRequiredService<IContentProvider>();
            ILogger<PortAudioStreamingMusicPlayer>? logger = sp.GetService<ILogger<PortAudioStreamingMusicPlayer>>();
            return new PortAudioStreamingMusicPlayer(audioRegistry, contentProvider, logger);
        });

        // Register audio service
        services.AddSingleton<IAudioService>(sp =>
        {
            AudioRegistry audioRegistry = sp.GetRequiredService<AudioRegistry>();
            ISoundEffectManager soundEffectManager = sp.GetRequiredService<ISoundEffectManager>();
            IMusicPlayer musicPlayer = sp.GetRequiredService<IMusicPlayer>();
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            AudioConfiguration audioConfig = sp.GetRequiredService<AudioConfiguration>();
            ILogger<AudioService>? logger = sp.GetService<ILogger<AudioService>>();

            var service = new AudioService(
                audioRegistry,
                soundEffectManager,
                musicPlayer,
                eventBus,
                audioConfig,
                logger);

            // Initialize the service (subscribes to events and enables playback)
            service.Initialize();

            return service;
        });

        // Note: MapMusicOrchestrator is created in InitializeMapMusicStep (needs World instance)
        // and stored in InitializationContext for lifecycle management

        return services;
    }
}
