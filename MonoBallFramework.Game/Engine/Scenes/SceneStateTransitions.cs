namespace MonoBallFramework.Game.Engine.Scenes;

/// <summary>
///     Validates scene state transitions according to the scene lifecycle.
///     Enforces valid state machine transitions to prevent invalid operations.
/// </summary>
public static class SceneStateTransitions
{
    /// <summary>
    ///     Validates whether a state transition is allowed.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The desired state.</param>
    /// <returns>True if the transition is valid, false otherwise.</returns>
    public static bool IsValidTransition(SceneState from, SceneState to)
    {
        return (from, to) switch
        {
            // Initialization flow
            (SceneState.Uninitialized, SceneState.Initializing) => true,
            (SceneState.Initializing, SceneState.Initialized) => true,

            // Content loading flow
            (SceneState.Initialized, SceneState.LoadingContent) => true,
            (SceneState.LoadingContent, SceneState.ContentLoaded) => true,

            // Running state
            (SceneState.ContentLoaded, SceneState.Running) => true,

            // Disposal from any state except Disposed
            (_, SceneState.Disposing) when from != SceneState.Disposed => true,
            (SceneState.Disposing, SceneState.Disposed) => true,

            // No other transitions allowed
            _ => false
        };
    }

    /// <summary>
    ///     Throws an exception if the transition is invalid.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The desired state.</param>
    /// <exception cref="InvalidOperationException">Thrown when transition is invalid.</exception>
    public static void ValidateTransition(SceneState from, SceneState to)
    {
        if (!IsValidTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid scene state transition: {from} → {to}. " +
                $"Valid transitions from {from}: {GetValidTransitions(from)}"
            );
        }
    }

    /// <summary>
    ///     Gets a human-readable list of valid transitions from a given state.
    /// </summary>
    /// <param name="from">The state to check.</param>
    /// <returns>A comma-separated list of valid target states.</returns>
    public static string GetValidTransitions(SceneState from)
    {
        var validStates = new List<SceneState>();

        foreach (SceneState to in Enum.GetValues<SceneState>())
        {
            if (IsValidTransition(from, to))
            {
                validStates.Add(to);
            }
        }

        return validStates.Count > 0
            ? string.Join(", ", validStates)
            : "None (terminal state)";
    }

    /// <summary>
    ///     Checks if a state allows Update() calls.
    /// </summary>
    public static bool CanUpdate(SceneState state)
    {
        return state == SceneState.Running;
    }

    /// <summary>
    ///     Checks if a state allows Draw() calls.
    /// </summary>
    public static bool CanDraw(SceneState state)
    {
        return state is SceneState.ContentLoaded or SceneState.Running;
    }

    /// <summary>
    ///     Checks if a state allows Initialize() to be called.
    /// </summary>
    public static bool CanInitialize(SceneState state)
    {
        return state == SceneState.Uninitialized;
    }

    /// <summary>
    ///     Checks if a state allows LoadContent() to be called.
    /// </summary>
    public static bool CanLoadContent(SceneState state)
    {
        return state == SceneState.Initialized;
    }
}
