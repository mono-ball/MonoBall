// Weather System Script - Example Test Mod
// Publishes weather events that other mods can react to

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.System;

public class WeatherScript : ScriptBase
{
    // Custom weather events
    public sealed record RainStartedEvent : IGameEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public int Intensity { get; init; } // 0-100
        public string MapId { get; init; } = string.Empty;
    }

    public sealed record RainStoppedEvent : IGameEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string MapId { get; init; } = string.Empty;
    }

    private int _tickCount = 0;
    private bool _isRaining = false;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to game tick
        On<TickEvent>(evt =>
        {
            _tickCount++;

            // Toggle rain every 300 ticks (5 seconds at 60 FPS)
            if (_tickCount % 300 == 0)
            {
                if (!_isRaining)
                {
                    StartRain();
                }
                else
                {
                    StopRain();
                }
            }
        }, priority: 500);
    }

    private void StartRain()
    {
        _isRaining = true;
        var intensity = Random.Shared.Next(30, 100);

        Context.Logger?.LogInformation(
            "[WeatherScript] Rain started (intensity: {Intensity})",
            intensity
        );

        Publish(new RainStartedEvent
        {
            Intensity = intensity,
            MapId = "route_1"
        });
    }

    private void StopRain()
    {
        _isRaining = false;

        Context.Logger?.LogInformation("[WeatherScript] Rain stopped");

        Publish(new RainStoppedEvent
        {
            MapId = "route_1"
        });
    }
}
