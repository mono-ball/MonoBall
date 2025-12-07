namespace MonoBallFramework.Game.Engine.Core.Events.Flags;

/// <summary>
///     Event raised when a game variable value changes.
///     Systems can subscribe to react to variable changes.
/// </summary>
public sealed class VariableChangedEvent : NotificationEventBase
{
    /// <summary>
    ///     The variable key that changed.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    ///     The previous value of the variable (null if newly created).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    ///     The new value of the variable (null if deleted).
    /// </summary>
    public string? NewValue { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        Key = string.Empty;
        OldValue = null;
        NewValue = null;
    }
}
