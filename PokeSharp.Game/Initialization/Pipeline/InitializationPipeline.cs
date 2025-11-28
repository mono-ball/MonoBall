using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline;

/// <summary>
///     Orchestrates the game initialization pipeline by executing initialization steps in order.
///     Provides extensibility through step registration and progress tracking.
/// </summary>
public class InitializationPipeline
{
    private readonly ILogger<InitializationPipeline> _logger;
    private readonly List<IInitializationStep> _steps = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializationPipeline" /> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public InitializationPipeline(ILogger<InitializationPipeline> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Adds an initialization step to the pipeline.
    ///     Steps are executed in the order they are added.
    /// </summary>
    /// <param name="step">The initialization step to add.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public InitializationPipeline AddStep(IInitializationStep step)
    {
        if (step == null)
        {
            throw new ArgumentNullException(nameof(step));
        }

        _steps.Add(step);
        _logger.LogDebug("Added initialization step: {StepName}", step.Name);
        return this;
    }

    /// <summary>
    ///     Executes all initialization steps in order.
    /// </summary>
    /// <param name="context">The initialization context.</param>
    /// <param name="progress">The progress tracker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    public async Task ExecuteAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken = default
    )
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (progress == null)
        {
            throw new ArgumentNullException(nameof(progress));
        }

        _logger.LogInformation(
            "Starting initialization pipeline with {StepCount} steps",
            _steps.Count
        );

        for (int i = 0; i < _steps.Count; i++)
        {
            IInitializationStep step = _steps[i];
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "Executing step {StepNumber}/{TotalSteps}: {StepName}",
                i + 1,
                _steps.Count,
                step.Name
            );

            try
            {
                progress.Report(step.Name, step.StartProgress);
                await step.ExecuteAsync(context, progress, cancellationToken);
                progress.Progress = step.EndProgress;

                _logger.LogDebug("Completed step: {StepName}", step.Name);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Initialization cancelled during step: {StepName}", step.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error executing initialization step {StepName}: {Message}",
                    step.Name,
                    ex.Message
                );
                throw;
            }
        }

        _logger.LogInformation("Initialization pipeline completed successfully");
    }
}
