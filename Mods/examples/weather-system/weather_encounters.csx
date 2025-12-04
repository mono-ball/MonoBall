#r "MonoBallFramework.Engine.Core.dll"
#load "events/WeatherEvents.csx"

using System;
using System.Collections.Generic;
using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Engine.Core.Scripting;

/// <summary>
/// Modifies Pokémon encounter rates and types based on current weather.
/// Subscribes to all weather events and adjusts spawns dynamically.
///
/// Weather Effects:
/// - Rain: Water-type spawns increase 1.5x
/// - Thunder: Electric-type spawns increase 2.0x, Water-types increase 1.3x
/// - Snow: Ice-type spawns increase 2.0x, Steel-types increase 1.2x
/// - Sunshine: Grass-type and Fire-type spawns increase 1.4x
/// - Clear: Normal spawn rates (baseline)
/// </summary>
public class WeatherEncounters : ScriptBase
{
    // Configuration value loaded from mod.json
    private float WeatherEncounterMultiplier =>
        Context.Configuration.GetValueOrDefault("weatherEncounterMultiplier", 1.5f);

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Context.Logger.LogInformation("Weather Encounters system initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Initialize state on first tick
        On<TickEvent>(evt =>
        {
            if (!Context.HasState<EncounterState>())
            {
                Context.World.Add(
                    Context.Entity.Value,
                    new EncounterState
                    {
                        CurrentWeather = "Clear",
                        TypeMultipliers = new Dictionary<string, float>(),
                        GlobalMultiplier = 1.0f,
                    }
                );
                Context.Logger.LogInformation(
                    $"Encounter state initialized (multiplier: {WeatherEncounterMultiplier})"
                );
            }
        });

        // Subscribe to all weather events
        On<RainStartedEvent>(OnRainStarted);
        On<RainStoppedEvent>(OnRainStopped);
        On<ThunderstrikeEvent>(OnThunderstrike);
        On<SnowStartedEvent>(OnSnowStarted);
        On<SunshineEvent>(OnSunshine);
        On<WeatherChangedEvent>(OnWeatherChanged);
        On<WeatherClearedEvent>(OnWeatherCleared);

        Context.Logger.LogInformation("Subscribed to weather events");
    }

    public override void OnUnload()
    {
        Context.Logger.LogInformation("Weather Encounters system shutting down");
        if (Context.HasState<EncounterState>())
        {
            ResetMultipliers();
            Context.RemoveState<EncounterState>();
        }
    }

    private void OnRainStarted(RainStartedEvent evt)
    {
        ref var state = ref Context.GetState<EncounterState>();

        Context.Logger.LogInformation(
            $"Rain weather active - adjusting encounters (intensity: {evt.Intensity:F2})"
        );

        state.CurrentWeather = "Rain";
        ResetMultipliers();

        // Water types spawn more in rain
        float waterMultiplier = 1.0f + (evt.Intensity * WeatherEncounterMultiplier);
        state.TypeMultipliers["Water"] = waterMultiplier;

        // Bug types also more active in rain
        state.TypeMultipliers["Bug"] = 1.0f + (evt.Intensity * 0.3f);

        // Fire types less common in rain
        state.TypeMultipliers["Fire"] = 1.0f - (evt.Intensity * 0.5f);

        ApplyEncounterMultipliers();

        Context.Logger.LogInformation($"Water-type encounter rate: {waterMultiplier:F2}x");
    }

    private void OnRainStopped(RainStoppedEvent evt)
    {
        ref var state = ref Context.GetState<EncounterState>();

        Context.Logger.LogInformation("Rain stopped - resetting encounter rates");

        if (state.CurrentWeather == "Rain" || state.CurrentWeather == "Thunder")
        {
            state.CurrentWeather = "Clear";
            ResetMultipliers();
            ApplyEncounterMultipliers();
        }
    }

    private void OnThunderstrike(ThunderstrikeEvent evt)
    {
        ref var state = ref Context.GetState<EncounterState>();

        // Thunder events indicate thunderstorm weather
        if (state.CurrentWeather != "Thunder")
        {
            Context.Logger.LogInformation("Thunderstorm active - electric types surging!");

            state.CurrentWeather = "Thunder";
            ResetMultipliers();

            // Electric types much more common in thunderstorms
            float electricMultiplier = 1.0f + (WeatherEncounterMultiplier * 1.5f); // 2.25x default
            state.TypeMultipliers["Electric"] = electricMultiplier;

            // Water types still boosted
            state.TypeMultipliers["Water"] = 1.0f + (WeatherEncounterMultiplier * 0.5f);

            // Flying types avoid thunderstorms
            state.TypeMultipliers["Flying"] = 0.4f;

            // Fire types very rare
            state.TypeMultipliers["Fire"] = 0.2f;

            ApplyEncounterMultipliers();

            Context.Logger.LogInformation(
                $"Electric-type encounter rate: {electricMultiplier:F2}x"
            );
        }
    }

    private void OnSnowStarted(SnowStartedEvent evt)
    {
        ref var state = ref Context.GetState<EncounterState>();

        Context.Logger.LogInformation(
            $"Snow weather active - ice types appearing (intensity: {evt.Intensity:F2})"
        );

        state.CurrentWeather = "Snow";
        ResetMultipliers();

        // Ice types much more common in snow
        float iceMultiplier = 1.0f + (evt.Intensity * WeatherEncounterMultiplier * 1.5f);
        state.TypeMultipliers["Ice"] = iceMultiplier;

        // Steel types more common in snow
        state.TypeMultipliers["Steel"] = 1.0f + (evt.Intensity * 0.3f);

        // Water types can appear (as ice-water types)
        state.TypeMultipliers["Water"] = 1.0f + (evt.Intensity * 0.2f);

        // Fire, Grass, Bug types much less common
        state.TypeMultipliers["Fire"] = 0.3f;
        state.TypeMultipliers["Grass"] = 0.5f;
        state.TypeMultipliers["Bug"] = 0.2f;

        ApplyEncounterMultipliers();

        Context.Logger.LogInformation($"Ice-type encounter rate: {iceMultiplier:F2}x");
    }

    private void OnSunshine(SunshineEvent evt)
    {
        ref var state = ref Context.GetState<EncounterState>();

        Context.Logger.LogInformation(
            $"Sunshine weather active - fire and grass types thriving (intensity: {evt.Intensity:F2})"
        );

        state.CurrentWeather = "Sunshine";
        ResetMultipliers();

        // Fire types more common in intense sunshine
        float fireMultiplier = 1.0f + (evt.Intensity * WeatherEncounterMultiplier * 0.8f);
        state.TypeMultipliers["Fire"] = fireMultiplier;

        // Grass types boosted if configured
        if (evt.BoostsGrassTypes)
        {
            float grassMultiplier = 1.0f + (evt.Intensity * WeatherEncounterMultiplier * 0.7f);
            state.TypeMultipliers["Grass"] = grassMultiplier;
        }

        // Bug types more active in sunshine
        state.TypeMultipliers["Bug"] = 1.0f + (evt.Intensity * 0.3f);

        // Water and Ice types less common
        state.TypeMultipliers["Water"] = 0.6f;
        state.TypeMultipliers["Ice"] = 0.3f;

        ApplyEncounterMultipliers();

        Context.Logger.LogInformation($"Fire-type encounter rate: {fireMultiplier:F2}x");
    }

    private void OnWeatherChanged(WeatherChangedEvent evt)
    {
        Context.Logger.LogInformation(
            $"Weather changed: {evt.PreviousWeather ?? "None"} -> {evt.NewWeather}"
        );

        // The specific weather events will handle multiplier changes
        // This is just for logging and tracking
    }

    private void OnWeatherCleared(WeatherClearedEvent evt)
    {
        ref var state = ref Context.GetState<EncounterState>();

        Context.Logger.LogInformation($"Weather cleared: {evt.ClearedWeather}");

        state.CurrentWeather = "Clear";
        ResetMultipliers();
        ApplyEncounterMultipliers();
    }

    private void ResetMultipliers()
    {
        ref var state = ref Context.GetState<EncounterState>();
        state.TypeMultipliers.Clear();
        state.GlobalMultiplier = 1.0f;
    }

    private void ApplyEncounterMultipliers()
    {
        ref var state = ref Context.GetState<EncounterState>();

        // In real implementation, would:
        // 1. Access the game's encounter system
        // 2. Modify spawn rates for each Pokémon type
        // 3. Update encounter tables based on multipliers

        Context.Logger.LogInformation(
            $"Applying encounter multipliers for {state.CurrentWeather} weather:"
        );

        foreach (var kvp in state.TypeMultipliers)
        {
            string pokemonType = kvp.Key;
            float multiplier = kvp.Value;

            Context.Logger.LogInformation($"  {pokemonType} type: {multiplier:F2}x");

            // Example: Would call game encounter system
            // EncounterManager.SetTypeMultiplier(pokemonType, multiplier);
        }

        // Example: Would update encounter tables
        // EncounterManager.RefreshEncounterTables();

        Context.Logger.LogInformation(
            $"Encounter multipliers applied for {state.TypeMultipliers.Count} types"
        );
    }

    /// <summary>
    /// Get the current spawn rate multiplier for a specific Pokémon type.
    /// </summary>
    public float GetTypeMultiplier(string pokemonType)
    {
        if (!Context.HasState<EncounterState>())
        {
            return 1.0f;
        }

        ref var state = ref Context.GetState<EncounterState>();
        if (state.TypeMultipliers.TryGetValue(pokemonType, out float multiplier))
        {
            return multiplier;
        }

        return 1.0f; // Default multiplier
    }

    /// <summary>
    /// Get all active type multipliers.
    /// </summary>
    public Dictionary<string, float> GetAllMultipliers()
    {
        if (!Context.HasState<EncounterState>())
        {
            return new Dictionary<string, float>();
        }

        ref var state = ref Context.GetState<EncounterState>();
        return new Dictionary<string, float>(state.TypeMultipliers);
    }

    /// <summary>
    /// Get current weather affecting encounters.
    /// </summary>
    public string GetCurrentWeather()
    {
        if (!Context.HasState<EncounterState>())
        {
            return "Clear";
        }

        ref var state = ref Context.GetState<EncounterState>();
        return state.CurrentWeather;
    }

    /// <summary>
    /// Calculate effective spawn chance for a Pokémon type.
    /// </summary>
    public float CalculateSpawnChance(string pokemonType, float baseChance)
    {
        if (!Context.HasState<EncounterState>())
        {
            return baseChance;
        }

        ref var state = ref Context.GetState<EncounterState>();
        float multiplier = GetTypeMultiplier(pokemonType);
        return baseChance * multiplier * state.GlobalMultiplier;
    }

    /// <summary>
    /// Manually set a type multiplier (for testing or special events).
    /// </summary>
    public void SetTypeMultiplier(string pokemonType, float multiplier)
    {
        if (!Context.HasState<EncounterState>())
        {
            return;
        }

        ref var state = ref Context.GetState<EncounterState>();
        state.TypeMultipliers[pokemonType] = multiplier;
        Context.Logger.LogInformation($"Manual multiplier set: {pokemonType} = {multiplier:F2}x");
        ApplyEncounterMultipliers();
    }
}

// Component to store encounter-specific state
public struct EncounterState
{
    public string CurrentWeather;
    public Dictionary<string, float> TypeMultipliers;
    public float GlobalMultiplier;
}

// Instantiate and return the weather encounters handler
return new WeatherEncounters();
