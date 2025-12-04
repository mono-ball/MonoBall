using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Types.Events;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Dialogue management service implementation.
///     Publishes dialogue events to the event bus for UI systems to handle.
/// </summary>
public class DialogueApiService(
    World world,
    IEventBus eventBus,
    ILogger<DialogueApiService> logger,
    IGameTimeService gameTime
) : IDialogueApi
{
    private readonly IEventBus _eventBus =
        eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly IGameTimeService _gameTime =
        gameTime ?? throw new ArgumentNullException(nameof(gameTime));

    private readonly ILogger<DialogueApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    /// <inheritdoc />
    public bool IsDialogueActive { get; private set; }

    /// <inheritdoc />
    public void ShowMessage(string message, string? speakerName = null, int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogOperationSkipped("Dialogue.ShowMessage", "message was null or whitespace");
            return;
        }

        var dialogueEvent = new DialogueRequestedEvent
        {
            TypeId = "dialogue-api",
            Timestamp = _gameTime.TotalSeconds,
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
    public void ClearMessages()
    {
        IsDialogueActive = false;
        _logger.LogDebug("Cleared dialogue messages");
    }
}
