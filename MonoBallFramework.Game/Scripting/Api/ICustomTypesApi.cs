using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     API for scripts to access custom type definitions from mods.
///     Provides type-safe and dynamic access to custom content types.
/// </summary>
public interface ICustomTypesApi
{
    /// <summary>
    ///     Gets a custom type definition by its full ID.
    /// </summary>
    /// <param name="id">The full ID (e.g., "weather:effect:rain").</param>
    /// <returns>The definition, or null if not found.</returns>
    ICustomTypeDefinition? GetDefinition(string id);

    /// <summary>
    ///     Gets a custom type definition by category and local ID.
    /// </summary>
    /// <param name="category">The content type category (e.g., "WeatherEffects").</param>
    /// <param name="localId">The local ID within the category (e.g., "rain").</param>
    /// <returns>The definition, or null if not found.</returns>
    ICustomTypeDefinition? GetDefinition(string category, string localId);

    /// <summary>
    ///     Gets all definitions for a custom type category.
    /// </summary>
    /// <param name="category">The content type category.</param>
    /// <returns>All definitions in that category.</returns>
    IEnumerable<ICustomTypeDefinition> GetAllDefinitions(string category);

    /// <summary>
    ///     Gets all registered custom type categories.
    /// </summary>
    /// <returns>List of category names.</returns>
    IReadOnlyCollection<string> GetCategories();

    /// <summary>
    ///     Checks if a custom type category is registered.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>True if the category exists.</returns>
    bool HasCategory(string category);

    /// <summary>
    ///     Gets the count of definitions in a category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>Number of definitions, or 0 if category doesn't exist.</returns>
    int GetDefinitionCount(string category);

    /// <summary>
    ///     Filters definitions by a predicate.
    /// </summary>
    /// <param name="category">The category to filter.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>Matching definitions.</returns>
    IEnumerable<ICustomTypeDefinition> Where(string category, Func<ICustomTypeDefinition, bool> predicate);
}
