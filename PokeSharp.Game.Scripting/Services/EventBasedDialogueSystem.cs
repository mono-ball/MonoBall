using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Event-based dialogue system that publishes dialogue requests to the event bus.
/// </summary>
/// <remarks>
///     This implementation allows decoupled dialogue handling - UI systems can subscribe
///     to DialogueRequestEvent and display messages in their own way.
/// </remarks>
public sealed class EventBasedDialogueSystem : IDialogueSystem
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventBasedDialogueSystem> _logger;
    private float _gameTime;

    /// <summary>
    ///     Initializes a new instance of the EventBasedDialogueSystem class.
    /// </summary>
    /// <param name="eventBus">The event bus for publishing dialogue requests.</param>
    /// <param name="logger">Logger instance.</param>
    public EventBasedDialogueSystem(IEventBus eventBus, ILogger<EventBasedDialogueSystem> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void ShowMessage(string message, string? speakerName = null, int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Attempted to show null or empty dialogue message");
            return;
        }

        var dialogueEvent = new DialogueRequestedEvent
        {
            TypeId = "dialogue-system",
            Timestamp = _gameTime,
            Message = message,
            SpeakerName = speakerName,
            Priority = priority,
        };

        _eventBus.Publish(dialogueEvent);
        IsDialogueActive = true;

        _logger.LogDebug(
            "Published dialogue request: {Message} (Speaker: {Speaker}, Priority: {Priority})",
            message,
            speakerName ?? "None",
            priority
        );
    }

    /// <inheritdoc />
    public bool IsDialogueActive { get; private set; }

    /// <inheritdoc />
    public void ClearMessages()
    {
        IsDialogueActive = false;
        _logger.LogDebug("Cleared dialogue messages");
    }

    /// <summary>
    ///     Update the game time for event timestamps.
    /// </summary>
    /// <param name="gameTime">Current game time in seconds.</param>
    public void UpdateGameTime(float gameTime)
    {
        _gameTime = gameTime;
    }
}
