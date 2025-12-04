namespace PokeSharp.Game.Scripting.HotReload.Notifications;

/// <summary>
///     In-game notification for hot-reload events (success, failure, warnings).
/// </summary>
public class HotReloadNotification
{
    public NotificationType Type { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan? Duration { get; init; }
    public int AffectedScripts { get; init; }
    public bool IsAutoDismiss { get; init; } = true;
}
