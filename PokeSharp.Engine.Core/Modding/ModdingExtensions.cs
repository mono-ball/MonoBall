using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
///     Extension methods for registering modding services
/// </summary>
public static class ModdingExtensions
{
    public static IServiceCollection AddModdingServices(
        this IServiceCollection services,
        string modsDirectory
    )
    {
        services.AddSingleton<PatchApplicator>();
        services.AddSingleton<PatchFileLoader>();
        services.AddSingleton<ModLoader>(sp =>
        {
            ILogger<ModLoader> logger = sp.GetRequiredService<ILogger<ModLoader>>();
            return new ModLoader(logger, modsDirectory);
        });

        return services;
    }
}
