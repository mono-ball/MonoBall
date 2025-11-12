using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Core.Templates;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Core.Modding;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Game.Services;
using PokeSharp.Game.Templates;
using PokeSharp.Game.Data.Factories;

namespace PokeSharp.Game;

/// <summary>
///     Extension methods for configuring game services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds all game services to the service collection.
    /// </summary>
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        // EF Core In-Memory Database for game data definitions
        // Register as Singleton since we're using In-Memory database for read-only data
        services.AddDbContext<PokeSharp.Game.Data.GameDataContext>(
            options =>
            {
                options.UseInMemoryDatabase("GameData");

                #if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
                #endif
            },
            ServiceLifetime.Singleton  // In-Memory DB can be singleton
        );

        // Data loading and services
        services.AddSingleton<PokeSharp.Game.Data.Loading.GameDataLoader>();
        services.AddSingleton<PokeSharp.Game.Data.Services.NpcDefinitionService>();
        services.AddSingleton<PokeSharp.Game.Data.Services.MapDefinitionService>();

        // NPC Sprite Loader - for loading sprites extracted from Pokemon Emerald
        services.AddSingleton<SpriteLoader>();

        // Core ECS
        services.AddSingleton(sp =>
        {
            var world = World.Create();
            return world;
        });

        // System Manager - Sequential execution (optimal for <500 entities per system)
        // Parallel overhead (1-2ms) exceeds work time (0.09ms) for Pokemon-style games
        services.AddSingleton<SystemManager>(sp =>
        {
            var logger = sp.GetService<ILogger<SystemManager>>();
            return new SystemManager(logger);
        });

        // Note: ComponentPoolManager registration removed - it was never used.
        // ECS systems work directly with component references via queries.
        // If temporary component copies are needed in the future, add it back.

        // Entity Pool Manager (Phase 4A) - For entity recycling and pooling
        services.AddSingleton(sp =>
        {
            var world = sp.GetRequiredService<World>();
            return new EntityPoolManager(world);
        });

        // Modding System - discovers and loads mods
        services.AddModdingServices("Mods");

        // Entity Factory & Templates

        // Component Deserializer Registry - for JSON template loading
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PokeSharp.Engine.Core.Templates.Loading.ComponentDeserializerRegistry>>();
            var registry = new PokeSharp.Engine.Core.Templates.Loading.ComponentDeserializerRegistry(logger);
            ComponentDeserializerSetup.RegisterAllDeserializers(registry, sp.GetService<ILogger>());
            return registry;
        });

        // JSON Template Loader - for loading templates from JSON files
        services.AddSingleton<PokeSharp.Engine.Core.Templates.Loading.JsonTemplateLoader>();

        // Template Cache - loads hardcoded + JSON templates + mod patches
        // Note: Requires running from bin directory (where Assets/ is copied)
        services.AddSingleton(sp =>
        {
            var cache = new TemplateCache();
            var logger = sp.GetService<ILogger<TemplateCache>>();

            // Load base game JSON templates as JSON (before deserialization)
            var jsonLoader = sp.GetRequiredService<PokeSharp.Engine.Core.Templates.Loading.JsonTemplateLoader>();
            var templateJsonCache = jsonLoader.LoadTemplateJsonAsync("Assets/Data/Templates", recursive: true).GetAwaiter().GetResult();

            logger?.LogInformation("WF  Template JSON loaded | count: {Count}, source: base", templateJsonCache.Count);

            // Load and apply mods
            var modLoader = sp.GetRequiredService<ModLoader>();
            var patchFileLoader = sp.GetRequiredService<PatchFileLoader>();
            var patchApplicator = sp.GetRequiredService<PatchApplicator>();
            var mods = modLoader.DiscoverMods();
            var sortedMods = modLoader.SortByLoadOrder(mods);

            logger?.LogInformation("WF  Mod system initializing | discovered: {Count}", sortedMods.Count);

            foreach (var mod in sortedMods)
            {
                logger?.LogInformation("WF  Loading mod | id: {ModId}, version: {Version}", mod.Manifest.ModId, mod.Manifest.Version);

                // Load mod templates as JSON (new content)
                if (mod.Manifest.ContentFolders.TryGetValue("Templates", out var templatesPath))
                {
                    var modTemplatesDir = mod.ResolvePath(templatesPath);
                    if (Directory.Exists(modTemplatesDir))
                    {
                        var modJsonCache = jsonLoader.LoadTemplateJsonAsync(modTemplatesDir, recursive: true).GetAwaiter().GetResult();

                        // Add mod templates to the main cache
                        foreach (var (path, json) in modJsonCache.GetAll())
                        {
                            templateJsonCache.Add(path, json);

                            // Extract templateId for logging
                            if (json is System.Text.Json.Nodes.JsonObject obj && obj.TryGetPropertyValue("templateId", out var idNode))
                            {
                                var templateId = idNode?.ToString().Trim('"');
                                logger?.LogInformation("    + {TemplateId}", templateId);
                            }
                        }
                    }
                }

                // Apply patches from mod (patch the JSON before deserialization)
                var patches = patchFileLoader.LoadModPatches(mod);
                foreach (var patch in patches)
                {
                    try
                    {
                        // Get the target template JSON
                        var targetJson = templateJsonCache.GetByTemplateId(patch.Target);
                        if (targetJson == null)
                        {
                            logger?.LogWarning("    ! Patch target not found | target: {Target}", patch.Target);
                            continue;
                        }

                        // Apply patch to JSON
                        var patchedJson = patchApplicator.ApplyPatch(targetJson, patch);
                        if (patchedJson == null)
                        {
                            logger?.LogWarning("    ! Patch failed | target: {Target}", patch.Target);
                            continue;
                        }

                        // Update the JSON cache with patched version
                        templateJsonCache.Update(patch.Target, patchedJson);
                        logger?.LogInformation("    * {Target} | {Desc}", patch.Target, patch.Description);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "    ! Patch error | target: {Target}", patch.Target);
                    }
                }
            }

            // Now deserialize all templates (base game + mods + patches applied)
            foreach (var (path, json) in templateJsonCache.GetAll())
            {
                try
                {
                    var template = jsonLoader.DeserializeTemplate(json, path);
                    cache.Register(template);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Template deserialization failed | path: {Path}", path);
                }
            }

            logger?.LogInformation("▶   Template cache ready | count: {Count}", cache.Count);

            return cache;
        });

        services.AddSingleton<IEntityFactoryService, EntityFactoryService>();

        // Behavior Registry
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TypeRegistry<BehaviorDefinition>>>();
            return new TypeRegistry<BehaviorDefinition>("Assets/Data/Behaviors", logger);
        });

        // Event Bus
        services.AddSingleton<IEventBus>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventBus>>();
            return new EventBus(logger);
        });

        // Property Mappers (for extensible Tiled property → ECS component mapping)
        services.AddPropertyMappers();

        // Abstract Factory Pattern: Graphics services that depend on GraphicsDevice
        // The factory allows deferred creation of AssetManager and MapLoader until
        // GraphicsDevice is available at runtime (in PokeSharpGame.Initialize)
        services.AddSingleton<IGraphicsServiceFactory, GraphicsServiceFactory>();

        // Game Time Service
        services.AddSingleton<IGameTimeService, GameTimeService>();

        // Collision Service - provides on-demand collision checking (not a system)
        services.AddSingleton<ICollisionService>(sp =>
        {
            var systemManager = sp.GetRequiredService<SystemManager>();
            // SpatialHashSystem is registered as a system and implements ISpatialQuery
            var spatialQuery = systemManager.GetSystem<SpatialHashSystem>();
            var logger = sp.GetService<ILogger<CollisionService>>();
            return new CollisionService(spatialQuery, logger);
        });

        // Scripting API Services
        services.AddSingleton<PlayerApiService>();
        services.AddSingleton<NpcApiService>();
        services.AddSingleton<MapApiService>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<MapApiService>>();
            // SpatialHashSystem is initialized later in GameInitializer
            // It will be set via SetSpatialQuery method after initialization
            return new MapApiService(world, logger);
        });
        services.AddSingleton<GameStateApiService>();
        services.AddSingleton<DialogueApiService>();
        services.AddSingleton<EffectApiService>();
        // WorldApi removed - scripts now use domain APIs directly via ScriptContext

        // Scripting API Provider
        services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();

        // Scripting Service
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ScriptService>>();
            var apis = sp.GetRequiredService<IScriptingApiProvider>();
            return new ScriptService("Assets/Scripts", logger, apis);
        });

        // Game Services Provider (Phase 4B facade)
        services.AddSingleton<IGameServicesProvider, GameServicesProvider>();

        // Logging Provider (Phase 5 facade)
        services.AddSingleton<ILoggingProvider, LoggingProvider>();

        // Initialization Provider (Phase 7 facade)
        services.AddSingleton<IInitializationProvider, InitializationProvider>();

        // Game Initializers and Helpers
        services.AddSingleton<PerformanceMonitor>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<PlayerFactory>();

        // Note: GameInitializer, MapInitializer, NPCBehaviorInitializer, and SpatialHashSystem
        // are created after GraphicsDevice is available in PokeSharpGame.Initialize()
        // AssetManager and MapLoader are now created via IGraphicsServiceFactory

        return services;
    }
}
