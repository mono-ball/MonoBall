using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline;

/// <summary>
///     Base class for initialization steps that provides common functionality.
/// </summary>
public abstract class InitializationStepBase : IInitializationStep
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializationStepBase" /> class.
    /// </summary>
    /// <param name="name">The name of this step.</param>
    /// <param name="startProgress">The progress value when this step starts (0.0 to 1.0).</param>
    /// <param name="endProgress">The progress value when this step completes (0.0 to 1.0).</param>
    protected InitializationStepBase(string name, float startProgress, float endProgress)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        StartProgress = startProgress;
        EndProgress = endProgress;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public float StartProgress { get; }

    /// <inheritdoc />
    public float EndProgress { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken = default
    )
    {
        progress.Report(Name, StartProgress);
        await ExecuteStepAsync(context, progress, cancellationToken);
        progress.Progress = EndProgress;
    }

    /// <summary>
    ///     Executes the actual initialization logic for this step.
    /// </summary>
    /// <param name="context">The initialization context.</param>
    /// <param name="progress">The progress tracker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    protected abstract Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    );
}
