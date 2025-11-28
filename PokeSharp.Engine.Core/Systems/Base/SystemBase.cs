using Arch.Core;

namespace PokeSharp.Engine.Core.Systems;

/// <summary>
///     Enhanced abstract base class for game systems with dependency injection support.
///     Provides common functionality and ensures proper initialization patterns.
///     Recommended base class for all new systems, especially those using DI.
/// </summary>
public abstract class SystemBase : ISystem
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

    /// <inheritdoc />
    public abstract void Update(World world, float deltaTime);

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
