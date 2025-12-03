using Arch.Core;
using PokeSharp.Engine.Core.Events;

namespace EnhancedLedges.Events;

/// <summary>
///     Event published when a player uses an item.
///     This would normally be part of the core engine events.
/// </summary>
public sealed record ItemUsedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that used the item.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the ID of the item that was used.
    /// </summary>
    public required string ItemId { get; init; }
}
