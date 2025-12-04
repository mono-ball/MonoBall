namespace MonoBallFramework.Game.Scripting.HotReload.Watchers.Events;

public class ScriptWatcherErrorEventArgs : EventArgs
{
    public Exception Exception { get; init; } = null!;
    public string Message { get; init; } = string.Empty;
    public bool IsCritical { get; init; }
}
