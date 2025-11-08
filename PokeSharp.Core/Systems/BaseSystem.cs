using Arch.Core;

namespace PokeSharp.Core.Systems;

/// <summary>
///     Abstract base class for game systems providing common functionality.
/// </summary>
public abstract class BaseSystem : ISystem
{
    /// <summary>
    ///     Gets the world instance this system operates on.
    /// </summary>
    protected World? World { get; private set; }

    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public virtual void Initialize(World world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <inheritdoc />
    public abstract void Update(World world, float deltaTime);

    /// <summary>
    ///     Helper method to check if the system is initialized.
    /// </summary>
    protected void EnsureInitialized()
    {
        if (World == null)
            throw new InvalidOperationException(
                $"System {GetType().Name} has not been initialized. Call Initialize() first."
            );
    }
}
