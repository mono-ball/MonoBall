using Microsoft.Extensions.DependencyInjection;

namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
///     Extension methods for registering the Content Provider system with dependency injection.
/// </summary>
public static class ContentExtensions
{
    /// <summary>
    ///     Adds the Content Provider system to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional action to configure content provider options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddContentProvider(
        this IServiceCollection services,
        Action<ContentProviderOptions>? configure = null)
    {
        // Configure options if provided
        services.Configure<ContentProviderOptions>(options => configure?.Invoke(options));

        // Register IContentProvider as singleton
        services.AddSingleton<IContentProvider, ContentProvider>();

        return services;
    }
}
