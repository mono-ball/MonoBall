using Arch.Core;

namespace PokeSharp.Core.Systems;

/// <summary>
///     Base interface for all game systems in the ECS architecture.
///     Systems contain logic that operates on entities with specific components.
/// </summary>
public interface ISystem
{
    /// <summary>
    ///     Gets the execution priority of this system.
    ///     Lower values execute first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Gets whether this system is currently enabled.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    ///     Initializes the system with the given world.
    ///     Called once before the first Update.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    void Initialize(World world);

    /// <summary>
    ///     Updates the system logic.
    ///     Called every frame.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    void Update(World world, float deltaTime);
}
