namespace PokeSharp.Engine.Core.Services;

/// <summary>
///     Provides information about whether a system is currently capturing input.
///     Used to prevent game input when debug console or other UI is active.
/// </summary>
/// <remarks>
///     This interface allows systems to check input capture state without
///     direct dependencies or reflection. Implementations should be lightweight
///     as this may be checked every frame.
/// </remarks>
public interface IInputCaptureProvider
{
    /// <summary>
    ///     Gets whether this provider is currently capturing input.
    ///     When true, game input should be blocked to prevent conflicts.
    /// </summary>
    bool IsCapturingInput { get; }
}
