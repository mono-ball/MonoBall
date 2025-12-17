using Arch.Core;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Interface for entity spawners that handle specific Tiled object types.
///     Each spawner is responsible for exactly one type of entity (Strategy Pattern).
///     Design principles:
///     - Single Responsibility: Each spawner handles one entity type only
///     - Fail-fast: Spawners throw on invalid data, never silently fail
///     - No fallbacks: If a spawner claims to handle an object, it must handle it completely
/// </summary>
public interface IEntitySpawner
{
    /// <summary>
    ///     Priority for spawner selection. Higher priority spawners are checked first.
    ///     Use this when multiple spawners could potentially handle the same object type.
    ///     Standard priorities:
    ///     - 100: Special case spawners (exact type match)
    ///     - 50: Standard spawners (category match)
    ///     - 0: Generic/fallback spawners (should be avoided per fail-fast principle)
    /// </summary>
    int Priority => 100;

    /// <summary>
    ///     Descriptive name for logging and debugging.
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    ///     Determines if this spawner can handle the given Tiled object.
    ///     Should be fast - only check type/properties, don't do validation.
    /// </summary>
    /// <param name="context">The spawn context containing the Tiled object.</param>
    /// <returns>True if this spawner can handle the object, false otherwise.</returns>
    bool CanSpawn(EntitySpawnContext context);

    /// <summary>
    ///     Spawns an entity from the Tiled object.
    ///     Pre-condition: CanSpawn() returned true for this context.
    ///     This method MUST throw on invalid data - never silently skip or use defaults
    ///     that would hide configuration errors.
    /// </summary>
    /// <param name="context">The spawn context with all required information.</param>
    /// <returns>The created entity.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when required properties are missing or have invalid values.
    /// </exception>
    Entity Spawn(EntitySpawnContext context);
}
