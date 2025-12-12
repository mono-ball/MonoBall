using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Services;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.GameData;

namespace MonoBallFramework.Game.Infrastructure.ServiceRegistration;

/// <summary>
/// Extension methods for registering audio services.
/// </summary>
public static class AudioServicesExtensions
{
    /// <summary>
    /// Registers NAudio-based audio services with the DI container.
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
            var contextFactory = sp.GetRequiredService<IDbContextFactory<GameDataContext>>();
            var logger = sp.GetRequiredService<ILogger<AudioRegistry>>();
            var registry = new AudioRegistry(contextFactory, logger);

            // Load audio definitions into cache for fast runtime access
            registry.LoadDefinitions();

            return registry;
        });

        // Register NAudio-based sound effect manager
        services.AddSingleton<INAudioSoundEffectManager>(sp =>
        {
            var audioRegistry = sp.GetRequiredService<AudioRegistry>();
            var audioConfig = sp.GetRequiredService<AudioConfiguration>();
            var logger = sp.GetService<ILogger<NAudioSoundEffectManager>>();

            return new NAudioSoundEffectManager(
                audioRegistry,
                audioConfig.MaxConcurrentSounds,
                logger);
        });

        // Register NAudio-based streaming music player
        // Streams audio on-demand (~64KB per stream vs ~32MB per cached track)
        services.AddSingleton<IMusicPlayer>(sp =>
        {
            var audioRegistry = sp.GetRequiredService<AudioRegistry>();
            var logger = sp.GetService<ILogger<NAudioStreamingMusicPlayer>>();
            return new NAudioStreamingMusicPlayer(audioRegistry, logger);
        });

        // Register NAudio-based audio service
        services.AddSingleton<IAudioService>(sp =>
        {
            var audioRegistry = sp.GetRequiredService<AudioRegistry>();
            var soundEffectManager = sp.GetRequiredService<INAudioSoundEffectManager>();
            var musicPlayer = sp.GetRequiredService<IMusicPlayer>();
            var eventBus = sp.GetRequiredService<IEventBus>();
            var audioConfig = sp.GetRequiredService<AudioConfiguration>();
            var logger = sp.GetService<ILogger<NAudioService>>();

            var service = new NAudioService(
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

        // Register Pokemon cry manager (NAudio-based)
        services.AddSingleton<IPokemonCryManager>(sp =>
        {
            var soundEffectManager = sp.GetRequiredService<INAudioSoundEffectManager>();
            var audioRegistry = sp.GetRequiredService<AudioRegistry>();
            var logger = sp.GetService<ILogger<PokemonCryManager>>();

            return new PokemonCryManager(
                soundEffectManager,
                audioRegistry,
                logger);
        });

        // Register battle audio manager
        services.AddSingleton<IBattleAudioManager>(sp =>
        {
            var audioService = sp.GetRequiredService<IAudioService>();
            var pokemonCryManager = sp.GetRequiredService<IPokemonCryManager>();

            return new BattleAudioManager(
                audioService,
                pokemonCryManager);
        });

        // Note: MapMusicOrchestrator is created in InitializeMapMusicStep (needs World instance)
        // and stored in InitializationContext for lifecycle management

        return services;
    }
}
