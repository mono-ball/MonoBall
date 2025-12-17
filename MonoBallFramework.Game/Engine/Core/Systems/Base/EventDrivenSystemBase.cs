using Arch.Core;

namespace MonoBallFramework.Game.Engine.Core.Systems.Base;

/// <summary>
///     Base class for event-driven systems that don't need per-frame updates.
///     Provides common functionality similar to SystemBase but without the abstract Update method.
/// </summary>
/// <remarks>
///     Use this base class for systems that respond to specific events (e.g. window resize)
///     rather than needing to update every frame.
/// </remarks>
public abstract class EventDrivenSystemBase : IEventDrivenSystem
{
    /// <summary>
    ///     Gets the world instance this system operates on.
    ///     Set during Initialize() and guaranteed to be non-null after initialization.
    /// </summary>
    protected World World { get; private set; } = null!;

    /// <summary>
    ///     Gets the name of this system (defaults to the class name).
    ///     Can be overridden for custom naming.
    /// </summary>
    protected virtual string SystemName => GetType().Name;

    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public virtual void Initialize(World world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        OnInitialized();
    }

    /// <summary>
    ///     Called after the world is set during initialization.
    ///     Override this to perform additional initialization logic with access to the World.
    /// </summary>
    protected virtual void OnInitialized() { }

    /// <summary>
    ///     Helper method to ensure the system is initialized before use.
    ///     Throws InvalidOperationException if Initialize has not been called.
    /// </summary>
    protected void EnsureInitialized()
    {
        if (World == null)
        {
            throw new InvalidOperationException(
                $"System {SystemName} has not been initialized. Call Initialize() first."
            );
        }
    }

    /// <summary>
    ///     Helper method to safely execute logic with automatic initialization check.
    ///     Prevents null reference exceptions from uninitialized World.
    /// </summary>
    protected void ExecuteIfInitialized(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (World != null)
        {
            action();
        }
    }

    /// <summary>
    ///     Helper method to safely execute logic with automatic initialization check and return value.
    /// </summary>
    protected TResult? ExecuteIfInitialized<TResult>(Func<TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return World != null ? func() : default;
    }
}
