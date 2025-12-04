#r "MonoBallFramework.Engine.Core.dll"
#load "events/WeatherEvents.csx"

using System;
using System.Linq;
using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Engine.Core.Scripting;

/// <summary>
/// Central weather controller that manages dynamic weather changes over time.
/// Publishes weather events that other mods can subscribe to.
///
/// Configuration:
/// - weatherChangeDurationMinutes: How often weather changes (default: 5)
/// - thunderProbabilityDuringRain: Chance of thunder during rain (default: 0.3)
/// - snowProbabilityInWinter: Chance of snow in winter months (default: 0.6)
/// </summary>
public class WeatherController : ScriptBase
{
    // Weather types
    private static readonly string[] WeatherTypes =
    {
        "Clear",
        "Rain",
        "Thunder",
        "Snow",
        "Sunshine",
        "Fog",
    };

    // Configuration values loaded from mod.json
    private int ChangeDurationMinutes =>
        Context.Configuration.GetValueOrDefault("weatherChangeDurationMinutes", 5);
    private float ThunderProbability =>
        Context.Configuration.GetValueOrDefault("thunderProbabilityDuringRain", 0.3f);
    private float SnowProbabilityWinter =>
        Context.Configuration.GetValueOrDefault("snowProbabilityInWinter", 0.6f);
    private bool EnableWeatherDamage =>
        Context.Configuration.GetValueOrDefault("enableWeatherDamage", true);

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Context.Logger.LogInformation("Weather Controller initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TickEvent>(evt =>
        {
            // Initialize state on first tick
            if (!Context.HasState<WeatherState>())
            {
                Context.World.Add(
                    Context.Entity.Value,
                    new WeatherState
                    {
                        CurrentWeather = "Clear",
                        TicksSinceLastChange = 0,
                        ChangeIntervalSeconds = ChangeDurationMinutes * 60f,
                        ThunderCheckTimer = 0f,
                        RandomSeed = DateTime.UtcNow.Millisecond,
                    }
                );

                // Publish initial sunshine event
                PublishWeatherEvent(
                    new SunshineEvent
                    {
                        WeatherType = "Sunshine",
                        Intensity = 0.7f,
                        DurationSeconds = ChangeDurationMinutes * 60,
                    }
                );

                Context.Logger.LogInformation("Initial weather set to Clear/Sunshine");
                return;
            }

            // Get state
            ref var state = ref Context.GetState<WeatherState>();
            var random = new Random(state.RandomSeed + (int)(state.TicksSinceLastChange * 1000));

            // Accumulate time
            state.TicksSinceLastChange += evt.DeltaTime;
            state.ThunderCheckTimer += evt.DeltaTime;

            // Check if it's time to change weather
            if (state.TicksSinceLastChange >= state.ChangeIntervalSeconds)
            {
                ChangeWeather(ref state, random);
                state.TicksSinceLastChange = 0;
                state.RandomSeed = DateTime.UtcNow.Millisecond;
            }

            // Check for thunder during rain (every 10 seconds)
            if (state.CurrentWeather == "Rain" && state.ThunderCheckTimer >= 10f)
            {
                if (random.NextDouble() < 0.1) // 10% chance per check
                {
                    TriggerThunderstrike(random);
                }
                state.ThunderCheckTimer = 0f;
            }
        });
    }

    public override void OnUnload()
    {
        Context.Logger.LogInformation("Weather Controller shutting down");
        if (Context.HasState<WeatherState>())
        {
            Context.RemoveState<WeatherState>();
        }
    }

    private void ChangeWeather(ref WeatherState state, Random random)
    {
        string? previousWeather = state.CurrentWeather;
        string newWeather = SelectNewWeather(random);

        // Stop previous weather
        if (previousWeather != null && previousWeather != "Clear")
        {
            StopWeather(previousWeather);
        }

        // Start new weather
        state.CurrentWeather = newWeather;
        StartWeather(newWeather, random);

        // Publish weather changed event
        PublishWeatherEvent(
            new WeatherChangedEvent
            {
                PreviousWeather = previousWeather,
                NewWeather = newWeather,
                IsNaturalTransition = true,
            }
        );

        Context.Logger.LogInformation(
            $"Weather changed: {previousWeather ?? "None"} -> {newWeather}"
        );
    }

    private string SelectNewWeather(Random random)
    {
        // Consider seasonal factors
        int month = DateTime.UtcNow.Month;
        bool isWinter = month == 12 || month == 1 || month == 2;
        bool isSummer = month >= 6 && month <= 8;

        // Weight different weather types
        double roll = random.NextDouble();

        if (isWinter && roll < SnowProbabilityWinter)
        {
            return "Snow";
        }
        else if (isSummer && roll < 0.4)
        {
            return "Sunshine";
        }
        else if (roll < 0.5)
        {
            return "Rain";
        }
        else if (roll < 0.7)
        {
            return "Sunshine";
        }
        else if (roll < 0.85)
        {
            return "Clear";
        }
        else
        {
            return "Fog";
        }
    }

    private void StartWeather(string weather, Random random)
    {
        int duration = ChangeDurationMinutes * 60;
        float intensity = 0.5f + (float)random.NextDouble() * 0.5f; // 0.5-1.0

        switch (weather)
        {
            case "Rain":
                PublishWeatherEvent(
                    new RainStartedEvent
                    {
                        WeatherType = "Rain",
                        Intensity = intensity,
                        DurationSeconds = duration,
                        CreatePuddles = true,
                        CanThunder = random.NextDouble() < ThunderProbability,
                    }
                );
                break;

            case "Thunder":
                // Thunder is treated as heavy rain with thunder enabled
                PublishWeatherEvent(
                    new RainStartedEvent
                    {
                        WeatherType = "Thunder",
                        Intensity = 0.9f,
                        DurationSeconds = duration,
                        CreatePuddles = true,
                        CanThunder = true,
                    }
                );
                break;

            case "Snow":
                PublishWeatherEvent(
                    new SnowStartedEvent
                    {
                        WeatherType = "Snow",
                        Intensity = intensity,
                        DurationSeconds = duration,
                        AccumulatesOnGround = true,
                        AccumulationRate = intensity * 0.5f,
                        MaxDepthLayers = 3,
                        CreatesIcyTerrain = intensity > 0.7f,
                    }
                );
                break;

            case "Sunshine":
                PublishWeatherEvent(
                    new SunshineEvent
                    {
                        WeatherType = "Sunshine",
                        Intensity = intensity,
                        DurationSeconds = duration,
                        BrightnessMultiplier = 1.0f + intensity * 0.5f,
                        AcceleratesEvaporation = true,
                        BoostsGrassTypes = true,
                        TemperatureBonus = intensity * 10.0f,
                    }
                );
                break;

            case "Clear":
            case "Fog":
                // These don't have specific start events yet
                Context.Logger.LogInformation($"Weather set to {weather}");
                break;
        }
    }

    private void StopWeather(string weather)
    {
        switch (weather)
        {
            case "Rain":
            case "Thunder":
                PublishWeatherEvent(
                    new RainStoppedEvent
                    {
                        WeatherType = weather,
                        Intensity = 0.0f,
                        PersistPuddles = true,
                        PuddleEvaporationSeconds = 120,
                    }
                );
                break;

            default:
                PublishWeatherEvent(
                    new WeatherClearedEvent { ClearedWeather = weather, ImmediateCleanup = false }
                );
                break;
        }
    }

    private void TriggerThunderstrike(Random random)
    {
        // Random position for lightning (would normally use actual map bounds)
        int x = random.Next(0, 100);
        int y = random.Next(0, 100);

        bool enableDamage = EnableWeatherDamage;

        PublishWeatherEvent(
            new ThunderstrikeEvent
            {
                WeatherType = "Thunder",
                Intensity = 1.0f,
                StrikePosition = (x, y),
                Damage = enableDamage ? 10 : 0,
                AffectRadius = 2,
                CausesEnvironmentalEffects = enableDamage,
            }
        );

        Context.Logger.LogInformation($"Thunder struck at ({x}, {y})!");
    }

    private void PublishWeatherEvent(IGameEvent weatherEvent)
    {
        Context.Events.Publish(weatherEvent);
    }

    /// <summary>
    /// Public method to manually change weather (can be called by other scripts).
    /// </summary>
    public void SetWeather(string weatherType, int durationSeconds = -1)
    {
        if (!WeatherTypes.Contains(weatherType))
        {
            Context.Logger.LogWarning($"Unknown weather type: {weatherType}");
            return;
        }

        if (!Context.HasState<WeatherState>())
        {
            return; // Not initialized yet
        }

        ref var state = ref Context.GetState<WeatherState>();
        string? previousWeather = state.CurrentWeather;

        if (previousWeather != null && previousWeather != "Clear")
        {
            StopWeather(previousWeather);
        }

        state.CurrentWeather = weatherType;
        state.TicksSinceLastChange = 0;

        if (durationSeconds > 0)
        {
            state.ChangeIntervalSeconds = durationSeconds;
        }

        var random = new Random(DateTime.UtcNow.Millisecond);
        StartWeather(weatherType, random);

        PublishWeatherEvent(
            new WeatherChangedEvent
            {
                PreviousWeather = previousWeather,
                NewWeather = weatherType,
                IsNaturalTransition = false,
            }
        );

        Context.Logger.LogInformation($"Weather manually set to {weatherType}");
    }

    /// <summary>
    /// Get current weather type.
    /// </summary>
    public string? GetCurrentWeather()
    {
        if (!Context.HasState<WeatherState>())
        {
            return null;
        }
        ref var state = ref Context.GetState<WeatherState>();
        return state.CurrentWeather;
    }
}

// Component to store weather-specific state
public struct WeatherState
{
    public string? CurrentWeather;
    public float TicksSinceLastChange;
    public float ChangeIntervalSeconds;
    public float ThunderCheckTimer;
    public int RandomSeed;
}

// Instantiate and return the controller
return new WeatherController();
