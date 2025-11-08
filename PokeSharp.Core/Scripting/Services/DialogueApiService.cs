using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Events;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Types.Events;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     Dialogue management service implementation.
///     Publishes dialogue events to the event bus for UI systems to handle.
/// </summary>
public class DialogueApiService(World world, IEventBus eventBus, ILogger<DialogueApiService> logger)
    : IDialogueApi
{
    private readonly IEventBus _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly ILogger<DialogueApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private bool _isDialogueActive;

    /// <inheritdoc />
    public bool IsDialogueActive => _isDialogueActive;

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
            TypeId = "dialogue-api",
            Timestamp = 0f, // TODO: Get from game time service
            Message = message,
            SpeakerName = speakerName,
            Priority = priority,
        };

        _eventBus.Publish(dialogueEvent);
        _isDialogueActive = true;

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
        _isDialogueActive = false;
        _logger.LogDebug("Cleared dialogue messages");
    }
}
