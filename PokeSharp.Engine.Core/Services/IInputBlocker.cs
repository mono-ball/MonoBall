namespace PokeSharp.Engine.Core.Services;

/// <summary>
///     Interface for services that can block input from reaching lower-priority systems.
///     Used by scenes to indicate when they have exclusive input (e.g., console, menus).
/// </summary>
public interface IInputBlocker
{
    /// <summary>
    ///     Gets a value indicating whether input is currently blocked.
    ///     When true, input systems should not process keyboard/gamepad input.
    /// </summary>
    bool IsInputBlocked { get; }
}
