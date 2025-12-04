namespace PokeSharp.Game.Engine.Scenes;

/// <summary>
///     Extension methods for <see cref="LoadingProgress" /> to simplify progress reporting.
/// </summary>
public static class LoadingProgressExtensions
{
    /// <summary>
    ///     Reports both the current step description and progress value atomically.
    ///     This is a convenience method to avoid setting CurrentStep and Progress separately.
    /// </summary>
    /// <param name="progress">The progress tracker instance.</param>
    /// <param name="currentStep">The description of the current initialization step.</param>
    /// <param name="progressValue">The progress value (0.0 to 1.0).</param>
    public static void Report(
        this LoadingProgress progress,
        string currentStep,
        float progressValue
    )
    {
        progress.CurrentStep = currentStep;
        progress.Progress = progressValue;
    }
}
