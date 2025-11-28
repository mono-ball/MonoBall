using Arch.Core;

namespace PokeSharp.Engine.Core.Systems;

/// <summary>
///     Interface for systems that perform rendering operations.
///     Render systems read component data and draw to the screen.
///     These systems execute during the Draw() phase of the game loop.
/// </summary>
public interface IRenderSystem : ISystem
{
    /// <summary>
    ///     Gets the order for render execution.
    ///     Lower values render first (background). Higher values render last (foreground).
    ///     Typical values: 0 (background), 1 (world), 2 (UI), 3 (debug).
    /// </summary>
    int RenderOrder { get; }

    /// <summary>
    ///     Renders the system's visual representation.
    ///     This method is called during the Draw phase of the game loop.
    /// </summary>
    /// <param name="world">The ECS world containing all entities to render.</param>
    void Render(World world);
}
