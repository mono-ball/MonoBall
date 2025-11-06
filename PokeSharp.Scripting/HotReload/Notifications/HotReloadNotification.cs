namespace PokeSharp.Scripting.HotReload.Notifications;

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

public enum NotificationType
{
    Success,
    Warning,
    Error,
    Info,
}

/// <summary>
///     Service for managing in-game hot-reload notifications.
/// </summary>
public interface IHotReloadNotificationService
{
    /// <summary>
    ///     Show a notification to the player.
    /// </summary>
    void ShowNotification(HotReloadNotification notification);

    /// <summary>
    ///     Clear all current notifications.
    /// </summary>
    void ClearNotifications();
}

/// <summary>
///     Console-based notification service (can be replaced with GUI later).
/// </summary>
public class ConsoleNotificationService : IHotReloadNotificationService
{
    public void ShowNotification(HotReloadNotification notification)
    {
        var icon = notification.Type switch
        {
            NotificationType.Success => "✓",
            NotificationType.Warning => "⚠",
            NotificationType.Error => "✗",
            NotificationType.Info => "ℹ",
            _ => "•",
        };

        var color = notification.Type switch
        {
            NotificationType.Success => ConsoleColor.Green,
            NotificationType.Warning => ConsoleColor.Yellow,
            NotificationType.Error => ConsoleColor.Red,
            NotificationType.Info => ConsoleColor.Cyan,
            _ => ConsoleColor.White,
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;

        var durationStr = notification.Duration.HasValue
            ? $" ({notification.Duration.Value.TotalMilliseconds:F0}ms)"
            : "";

        var affectedStr =
            notification.AffectedScripts > 0 ? $" [{notification.AffectedScripts} scripts]" : "";

        Console.WriteLine($"[HOT-RELOAD] {icon} {notification.Message}{durationStr}{affectedStr}");

        if (!string.IsNullOrEmpty(notification.Details))
            Console.WriteLine($"            {notification.Details}");

        Console.ForegroundColor = originalColor;
    }

    public void ClearNotifications()
    {
        // Console doesn't need clearing
    }
}
