namespace PokeSharp.Game.Scripting.HotReload;

public class ScriptChangedEventArgs : EventArgs
{
    public string FilePath { get; init; } = string.Empty;
    public DateTime ChangeTime { get; init; }
    public long FileSize { get; init; }
    public string ChangeType { get; init; } = "Modified";
}
