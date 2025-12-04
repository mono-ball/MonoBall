using System;
using Arch.Core;
using MonoBallFramework.Engine.Core.Events;

/// <summary>
/// Custom event published when an NPC offers a quest to the player.
/// Other scripts can listen to this to show UI, play sounds, etc.
/// </summary>
public sealed record QuestOfferedEvent : IGameEvent, IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>The NPC entity offering the quest.</summary>
    public required Entity Entity { get; init; }

    /// <summary>Unique identifier of the quest being offered.</summary>
    public required string QuestId { get; init; }

    /// <summary>The player entity being offered the quest.</summary>
    public required Entity PlayerId { get; init; }
}

/// <summary>
/// Custom event published when a player accepts a quest.
/// Quest managers listen to this to activate the quest.
/// </summary>
public sealed record QuestAcceptedEvent : IGameEvent, IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>The player entity accepting the quest.</summary>
    public required Entity Entity { get; init; }

    /// <summary>Unique identifier of the quest being accepted.</summary>
    public required string QuestId { get; init; }
}

/// <summary>
/// Custom event published when quest progress changes.
/// UI components listen to this to update quest trackers.
/// </summary>
public sealed record QuestUpdatedEvent : IGameEvent, IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>The player entity whose quest was updated.</summary>
    public required Entity Entity { get; init; }

    /// <summary>Unique identifier of the quest being updated.</summary>
    public required string QuestId { get; init; }

    /// <summary>Current progress (e.g., 3/5 Pokemon caught).</summary>
    public required int Progress { get; init; }

    /// <summary>Target progress (e.g., 5 for "catch 5 Pokemon").</summary>
    public required int Target { get; init; }

    /// <summary>Description of the progress update.</summary>
    public string? ProgressDescription { get; init; }
}

/// <summary>
/// Custom event published when a quest is completed.
/// Reward handlers listen to this to give items, money, etc.
/// </summary>
public sealed record QuestCompletedEvent : IGameEvent, IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>The player entity who completed the quest.</summary>
    public required Entity Entity { get; init; }

    /// <summary>Unique identifier of the quest completed.</summary>
    public required string QuestId { get; init; }

    /// <summary>Rewards to be given (items, money, etc.).</summary>
    public required Dictionary<string, object> Rewards { get; init; }
}

/// <summary>
/// Custom event published when a quest fails or is abandoned.
/// Quest managers listen to this to clean up quest state.
/// </summary>
public sealed record QuestFailedEvent : IGameEvent, IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>The player entity whose quest failed.</summary>
    public required Entity Entity { get; init; }

    /// <summary>Unique identifier of the quest that failed.</summary>
    public required string QuestId { get; init; }

    /// <summary>Reason for failure (timeout, abandoned, etc.).</summary>
    public required string Reason { get; init; }
}

// No return statement needed - this file only defines event types
