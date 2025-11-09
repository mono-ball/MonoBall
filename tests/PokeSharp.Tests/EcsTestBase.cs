using Arch.Core;
using PokeSharp.Core.Systems;
using Xunit;

namespace PokeSharp.Tests.ECS;

/// <summary>
///     Base class for all ECS-related tests providing common setup and teardown.
///     Automatically creates and disposes a World instance for each test.
/// </summary>
public abstract class EcsTestBase : IDisposable
{
    private bool _disposed;

    /// <summary>
    ///     Gets the ECS World instance for testing.
    ///     Automatically created for each test and disposed after test completion.
    /// </summary>
    protected World World { get; private set; }

    /// <summary>
    ///     Gets the SystemManager instance for testing.
    ///     Automatically created for each test and can be initialized with systems.
    /// </summary>
    protected SystemManager SystemManager { get; private set; }

    /// <summary>
    ///     Initializes a new instance of the test base.
    ///     Creates a fresh World and SystemManager for the test.
    /// </summary>
    protected EcsTestBase()
    {
        World = World.Create();
        SystemManager = new SystemManager();
    }

    /// <summary>
    ///     Performs cleanup after each test.
    ///     Disposes the World to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Disposes resources used by the test.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            World?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    ///     Helper method to run systems for testing.
    ///     Initializes the SystemManager if not already initialized.
    /// </summary>
    /// <param name="deltaTime">Time delta for the update (default: 0.016f for 60 FPS).</param>
    protected void RunSystems(float deltaTime = 0.016f)
    {
        // Initialize if not already initialized
        try
        {
            SystemManager.Update(World, deltaTime);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not been initialized"))
        {
            SystemManager.Initialize(World);
            SystemManager.Update(World, deltaTime);
        }
    }

    /// <summary>
    ///     Helper method to run systems multiple times.
    ///     Useful for testing behavior over multiple frames.
    /// </summary>
    /// <param name="frameCount">Number of frames to simulate.</param>
    /// <param name="deltaTime">Time delta per frame (default: 0.016f for 60 FPS).</param>
    protected void RunSystemsForFrames(int frameCount, float deltaTime = 0.016f)
    {
        for (int i = 0; i < frameCount; i++)
        {
            RunSystems(deltaTime);
        }
    }

    /// <summary>
    ///     Creates a test entity with no components.
    ///     Useful for adding components later in tests.
    /// </summary>
    /// <returns>The created entity.</returns>
    protected Entity CreateEntity()
    {
        return World.Create();
    }

    /// <summary>
    ///     Gets the count of entities in the world.
    /// </summary>
    /// <returns>Number of entities.</returns>
    protected int GetEntityCount()
    {
        return World.CountEntities();
    }

    /// <summary>
    ///     Asserts that an entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity to check.</param>
    protected void AssertHasComponent<T>(Entity entity) where T : struct
    {
        Assert.True(World.Has<T>(entity), $"Entity should have component {typeof(T).Name}");
    }

    /// <summary>
    ///     Asserts that an entity does not have a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity to check.</param>
    protected void AssertDoesNotHaveComponent<T>(Entity entity) where T : struct
    {
        Assert.False(World.Has<T>(entity), $"Entity should not have component {typeof(T).Name}");
    }
}
