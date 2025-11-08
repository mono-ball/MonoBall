namespace PokeSharp.Scripting.HotReload.Notifications;

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
