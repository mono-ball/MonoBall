using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline;

/// <summary>
///     Represents a single step in the game initialization pipeline.
///     Each step handles a specific phase of initialization (e.g., loading data, initializing systems).
/// </summary>
public interface IInitializationStep
{
    /// <summary>
    ///     Gets the name of this initialization step (for logging and progress reporting).
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the progress value when this step starts (0.0 to 1.0).
    /// </summary>
    float StartProgress { get; }

    /// <summary>
    ///     Gets the progress value when this step completes (0.0 to 1.0).
    /// </summary>
    float EndProgress { get; }

    /// <summary>
    ///     Executes this initialization step.
    /// </summary>
    /// <param name="context">The initialization context containing shared state.</param>
    /// <param name="progress">The progress tracker for reporting initialization progress.</param>
    /// <param name="cancellationToken">Cancellation token to cancel initialization.</param>
    /// <returns>Task representing the async operation.</returns>
    Task ExecuteAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken = default
    );
}
