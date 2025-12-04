using Arch.Core;
using PokeSharp.Game.Engine.Common.Validation;

namespace PokeSharp.Game.Engine.Systems.Factories;

/// <summary>
///     Service for spawning Arch ECS entities from templates.
///     Provides type-safe entity creation with validation and configuration.
/// </summary>
public interface IEntityFactoryService
{
    /// <summary>
    ///     Spawn an entity from a template with spawn context.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="world">Arch world to spawn entity in</param>
    /// <param name="context">Spawn context (position, overrides)</param>
    /// <returns>Spawned entity</returns>
    /// <exception cref="ArgumentException">Template not found or invalid</exception>
    Entity SpawnFromTemplate(string templateId, World world, EntitySpawnContext? context = null);

    /// <summary>
    ///     Spawn an entity with fluent configuration.
    ///     Allows inline component overrides via EntityBuilder.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="world">Arch world</param>
    /// <param name="configure">Builder configuration action</param>
    /// <returns>Spawned entity</returns>
    Entity SpawnFromTemplate(string templateId, World world, Action<EntityBuilder> configure);

    /// <summary>
    ///     Spawn multiple entities from templates in batch (optimized).
    ///     More efficient than spawning individually when spawning many entities.
    /// </summary>
    /// <param name="templateIds">Template identifiers</param>
    /// <param name="world">Arch world</param>
    /// <returns>Spawned entities</returns>
    IEnumerable<Entity> SpawnBatch(IEnumerable<string> templateIds, World world);

    /// <summary>
    ///     Validate a template before spawning.
    ///     Checks template existence, component compatibility, and data correctness.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <returns>Validation result with Context set to templateId</returns>
    ValidationResult ValidateTemplate(string templateId);

    /// <summary>
    ///     Check if a template exists and is ready for spawning.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <returns>True if template exists</returns>
    bool HasTemplate(string templateId);

    /// <summary>
    ///     Get all available template IDs for a given tag.
    ///     Example: GetTemplateIdsByTag("pokemon") returns all Pokemon templates.
    /// </summary>
    /// <param name="tag">Entity tag to filter by</param>
    /// <returns>Template IDs matching the tag</returns>
    IEnumerable<string> GetTemplateIdsByTag(string tag);

    /// <summary>
    ///     Spawn multiple entities from the same template efficiently.
    ///     Optimized for batch spawning by resolving template hierarchy and validation once.
    /// </summary>
    /// <param name="templateId">Template ID to spawn from</param>
    /// <param name="world">World to spawn entities in</param>
    /// <param name="count">Number of entities to spawn</param>
    /// <param name="configureEach">Optional per-entity configuration with index</param>
    /// <returns>Array of spawned entities</returns>
    Entity[] SpawnBatchFromTemplate(
        string templateId,
        World world,
        int count,
        Action<EntityBuilder, int>? configureEach = null
    );

    /// <summary>
    ///     Release multiple entities (currently destroys them, placeholder for future pooling).
    /// </summary>
    /// <param name="entities">Entities to release</param>
    /// <param name="world">World to destroy entities in</param>
    void ReleaseBatch(Entity[] entities, World world);
}
