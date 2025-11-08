using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Events;
using PokeSharp.Core.Types.Events;

namespace PokeSharp.Game.Diagnostics;

/// <summary>
/// Test subscriber for validating Phase 1 API event publishing.
/// Logs all dialogue and effect events to console for verification.
/// </summary>
public class ApiTestEventSubscriber : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ApiTestEventSubscriber> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private int _dialogueCount = 0;
    private int _effectCount = 0;

    public ApiTestEventSubscriber(World world, IEventBus eventBus, ILogger<ApiTestEventSubscriber> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to dialogue events
        var dialogueSub = _eventBus.Subscribe<DialogueRequestedEvent>(OnDialogueRequested);
        _subscriptions.Add(dialogueSub);

        // Subscribe to effect events
        var effectSub = _eventBus.Subscribe<EffectRequestedEvent>(OnEffectRequested);
        _subscriptions.Add(effectSub);

        _logger.LogInformation("✅ ApiTestEventSubscriber initialized - listening for events");
    }

    private void OnDialogueRequested(DialogueRequestedEvent evt)
    {
        _dialogueCount++;

        _logger.LogInformation(
            "📨 DIALOGUE EVENT #{Count}: \"{Message}\" (Speaker: {Speaker}, Priority: {Priority})",
            _dialogueCount,
            evt.Message,
            evt.SpeakerName ?? "None",
            evt.Priority
        );

        // Additional validation
        if (string.IsNullOrWhiteSpace(evt.Message))
        {
            _logger.LogWarning("⚠️ Received dialogue event with empty message!");
        }
    }

    private void OnEffectRequested(EffectRequestedEvent evt)
    {
        _effectCount++;

        var tintInfo = evt.Tint.HasValue
            ? $"Tint: ({evt.Tint.Value.R}, {evt.Tint.Value.G}, {evt.Tint.Value.B})"
            : "No tint";

        _logger.LogInformation(
            "✨ EFFECT EVENT #{Count}: \"{EffectId}\" at ({X}, {Y}) - Duration: {Duration}s, Scale: {Scale}, {Tint}",
            _effectCount,
            evt.EffectId,
            evt.Position.X,
            evt.Position.Y,
            evt.Duration,
            evt.Scale,
            tintInfo
        );

        // Additional validation
        if (string.IsNullOrWhiteSpace(evt.EffectId))
        {
            _logger.LogWarning("⚠️ Received effect event with empty effectId!");
        }
    }

    public void Dispose()
    {
        _logger.LogInformation(
            "🔄 ApiTestEventSubscriber shutting down - Processed {DialogueCount} dialogue events and {EffectCount} effect events",
            _dialogueCount,
            _effectCount
        );

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}
