using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
///     Extension methods for registering modding services.
///     Registers all modding services: patches, scripts, content folders, and code mods.
/// </summary>
public static class ModdingExtensions
{
    /// <summary>
    ///     Registers unified modding services.
    ///     Requires ScriptService, World, IEventBus, and IScriptingApiProvider to be registered.
    /// </summary>
    public static IServiceCollection AddModdingServices(
        this IServiceCollection services,
        string gameBasePath
    )
    {
        services.AddSingleton<PatchApplicator>();
        services.AddSingleton<PatchFileLoader>();

        // Register CustomTypesApiService for mods to declare custom content types
        services.AddSingleton<CustomTypesApiService>();

        // Register ICustomTypesApi interface pointing to the same CustomTypesApiService singleton
        services.AddSingleton<ICustomTypesApi>(sp => sp.GetRequiredService<CustomTypesApiService>());

        // Register CustomTypeSchemaValidator for validating custom type definitions
        services.AddSingleton<CustomTypeSchemaValidator>();

        services.AddSingleton<ModLoader>(sp =>
        {
            ILogger<ModLoader> logger = sp.GetRequiredService<ILogger<ModLoader>>();
            ScriptService scriptService = sp.GetRequiredService<ScriptService>();
            World world = sp.GetRequiredService<World>();
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IScriptingApiProvider apis = sp.GetRequiredService<IScriptingApiProvider>();
            PatchApplicator patchApplicator = sp.GetRequiredService<PatchApplicator>();
            PatchFileLoader patchFileLoader = sp.GetRequiredService<PatchFileLoader>();
            CustomTypesApiService customTypesService = sp.GetRequiredService<CustomTypesApiService>();
            CustomTypeSchemaValidator schemaValidator = sp.GetRequiredService<CustomTypeSchemaValidator>();
            return new ModLoader(
                scriptService,
                logger,
                world,
                eventBus,
                apis,
                patchApplicator,
                patchFileLoader,
                gameBasePath,
                customTypesService,
                schemaValidator
            );
        });

        // Register IModLoader interface pointing to the same ModLoader singleton instance
        services.AddSingleton<IModLoader>(sp => sp.GetRequiredService<ModLoader>());

        return services;
    }
}
