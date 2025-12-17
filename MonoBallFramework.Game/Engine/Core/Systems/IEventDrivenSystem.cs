using Arch.Core;

namespace MonoBallFramework.Game.Engine.Core.Systems;

/// <summary>
///     Interface for systems that respond to events rather than per-frame updates.
///     Event-driven systems don't need Update() called every frame.
/// </summary>
/// <remarks>
///     <para>
///         <b>Use Cases:</b>
///         - Window resize handling (CameraViewportSystem)
///         - Scene transitions (triggered by scene manager)
///         - Configuration changes
///         - One-time initialization systems
///     </para>
///     <para>
///         <b>Benefits:</b>
///         - Avoids empty Update() methods
///         - Clear intent: system is event-driven
///         - Can be excluded from update loop
///         - Better performance (no per-frame overhead)
///     </para>
///     <para>
///         <b>Comparison with IUpdateSystem:</b>
///         - IUpdateSystem: Called every frame (60+ times/second)
///         - IEventDrivenSystem: Called only when event occurs
///     </para>
/// </remarks>
/// <example>
///     <code>
/// public class CameraViewportSystem : IEventDrivenSystem
/// {
///     public void HandleResize(World world, int width, int height)
///     {
///         // Only called when window resizes
///     }
/// }
/// </code>
/// </example>
public interface IEventDrivenSystem
{
    /// <summary>
    ///     Gets or sets whether this system is enabled.
    ///     Disabled systems should not process events.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    ///     Gets the system priority for initialization order.
    ///     Lower values initialize first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Initializes the system with the given world.
    ///     Called once when the system is registered.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    void Initialize(World world);
}
