using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<ModLoader>(sp =>
        {
            ILogger<ModLoader> logger = sp.GetRequiredService<ILogger<ModLoader>>();
            Scripting.Services.ScriptService scriptService = sp.GetRequiredService<Scripting.Services.ScriptService>();
            Arch.Core.World world = sp.GetRequiredService<Arch.Core.World>();
            Engine.Core.Events.IEventBus eventBus = sp.GetRequiredService<Engine.Core.Events.IEventBus>();
            Scripting.Api.IScriptingApiProvider apis = sp.GetRequiredService<Scripting.Api.IScriptingApiProvider>();
            PatchApplicator patchApplicator = sp.GetRequiredService<PatchApplicator>();
            PatchFileLoader patchFileLoader = sp.GetRequiredService<PatchFileLoader>();
            return new ModLoader(
                scriptService,
                logger,
                world,
                eventBus,
                apis,
                patchApplicator,
                patchFileLoader,
                gameBasePath
            );
        });

        return services;
    }
}
