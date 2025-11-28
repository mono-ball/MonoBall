using Arch.Core;

namespace PokeSharp.Engine.Core.Systems;

/// <summary>
///     Interface for systems that perform game logic updates.
///     Update systems modify component data and game state.
///     These systems execute during the Update() phase of the game loop.
/// </summary>
/// <remarks>
///     Update systems use the Priority property inherited from ISystem for execution ordering.
///     Lower values execute first. Typical range: 0-1000.
/// </remarks>
public interface IUpdateSystem : ISystem
{
    /// <summary>
    ///     Updates the system logic for the current frame.
    ///     This method is called during the Update phase of the game loop.
    /// </summary>
    /// <param name="world">The ECS world containing all entities.</param>
    /// <param name="deltaTime">Time elapsed since the last update, in seconds.</param>
    new void Update(World world, float deltaTime);
}
