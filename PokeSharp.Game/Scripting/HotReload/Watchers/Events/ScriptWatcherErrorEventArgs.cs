namespace PokeSharp.Game.Scripting.HotReload;

public class ScriptWatcherErrorEventArgs : EventArgs
{
    public Exception Exception { get; init; } = null!;
    public string Message { get; init; } = string.Empty;
    public bool IsCritical { get; init; }
}
