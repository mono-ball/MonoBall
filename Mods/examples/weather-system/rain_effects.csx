#r "PokeSharp.Engine.Core.dll"
#load "events/WeatherEvents.csx"

using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.System;
using PokeSharp.Engine.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles visual and audio effects for rain weather.
/// Subscribes to RainStartedEvent and RainStoppedEvent.
/// Creates puddles on walkable tiles and manages rain particle effects.
/// </summary>
public class RainEffects : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Context.Logger.LogInformation("Rain Effects system initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Initialize state on first tick
        On<TickEvent>(evt =>
        {
            if (!Context.HasState<RainState>())
            {
                Context.World.Add(
                    Context.Entity.Value,
                    new RainState
                    {
                        IsRaining = false,
                        RainIntensity = 0.0f,
                        PuddlePositions = new HashSet<(int, int)>(),
                        EvaporationTimer = 0f,
                        EvaporationDuration = 0f,
                        IsEvaporating = false
                    }
                );
                Context.Logger.LogInformation("Rain state initialized");
            }

            // Handle puddle evaporation if active
            ref var state = ref Context.GetState<RainState>();
            if (state.IsEvaporating && state.PuddlePositions.Count > 0)
            {
                state.EvaporationTimer += evt.DeltaTime;

                // Evaporate puddles gradually
                if (state.EvaporationTimer >= state.EvaporationDuration / state.PuddlePositions.Count)
                {
                    var puddle = state.PuddlePositions.First();
                    RemovePuddle(puddle.Item1, puddle.Item2);
                    state.EvaporationTimer = 0f;
                }
            }
        });

        // Subscribe to rain events
        On<RainStartedEvent>(OnRainStarted);
        On<RainStoppedEvent>(OnRainStopped);
        On<SunshineEvent>(OnSunshine);

        Context.Logger.LogInformation("Subscribed to rain weather events");
    }

    public override void OnUnload()
    {
        Context.Logger.LogInformation("Rain Effects system shutting down");

        if (Context.HasState<RainState>())
        {
            ref var state = ref Context.GetState<RainState>();

            // Clean up effects
            if (state.IsRaining)
            {
                StopRainEffects();
            }

            ClearAllPuddles();
            Context.RemoveState<RainState>();
        }
    }

    private void OnRainStarted(RainStartedEvent evt)
    {
        ref var state = ref Context.GetState<RainState>();

        Context.Logger.LogInformation($"Rain started! Intensity: {evt.Intensity:F2}, Duration: {evt.DurationSeconds}s");

        state.IsRaining = true;
        state.RainIntensity = evt.Intensity;

        // Start visual rain effects
        StartRainEffects(evt.Intensity);

        // Play rain sound
        PlayRainSound(evt.Intensity);

        // Create puddles if enabled
        if (evt.CreatePuddles)
        {
            StartCreatingPuddles(evt.Intensity);
        }

        // Log thunder capability
        if (evt.CanThunder)
        {
            Context.Logger.LogInformation("Thunderstorm possible during this rain");
        }
    }

    private void OnRainStopped(RainStoppedEvent evt)
    {
        ref var state = ref Context.GetState<RainState>();

        Context.Logger.LogInformation($"Rain stopped. Puddles persist: {evt.PersistPuddles}");

        state.IsRaining = false;
        state.RainIntensity = 0.0f;

        // Stop rain effects
        StopRainEffects();

        // Stop rain sound
        StopRainSound();

        // Handle puddle persistence
        if (evt.PersistPuddles)
        {
            SchedulePuddleEvaporation(evt.PuddleEvaporationSeconds);
        }
        else
        {
            ClearAllPuddles();
        }
    }

    private void OnSunshine(SunshineEvent evt)
    {
        ref var state = ref Context.GetState<RainState>();

        // Accelerate puddle evaporation in sunshine
        if (evt.AcceleratesEvaporation && state.PuddlePositions.Count > 0)
        {
            Context.Logger.LogInformation("Sunshine accelerating puddle evaporation");

            // Reduce evaporation time
            int acceleratedTime = Math.Max(30, 120 - (int)(evt.Intensity * 60));
            SchedulePuddleEvaporation(acceleratedTime);
        }
    }

    private void StartRainEffects(float intensity)
    {
        // In a real implementation, this would:
        // 1. Create particle systems for rain droplets
        // 2. Adjust particle count based on intensity
        // 3. Add splash effects when droplets hit ground
        // 4. Darken the lighting/sky

        Context.Logger.LogInformation($"Starting rain particle effects (intensity: {intensity:F2})");

        // Calculate particle count based on intensity
        int particleCount = (int)(intensity * 500); // 0-500 particles

        // Example: Would call game engine's particle system
        // ParticleSystem.Create("rain_droplets", particleCount);
        // ParticleSystem.SetVelocity(new Vector2(0, -5 * intensity));
        // Lighting.SetBrightness(1.0f - intensity * 0.3f);

        Context.Logger.LogInformation($"Rain particles created: {particleCount}");
    }

    private void StopRainEffects()
    {
        Context.Logger.LogInformation("Stopping rain particle effects");

        // Example: Would call game engine
        // ParticleSystem.Destroy("rain_droplets");
        // Lighting.SetBrightness(1.0f);

        Context.Logger.LogInformation("Rain visual effects stopped");
    }

    private void PlayRainSound(float intensity)
    {
        // In a real implementation, this would:
        // 1. Load rain ambient sound
        // 2. Adjust volume based on intensity
        // 3. Loop the sound

        Context.Logger.LogInformation($"Playing rain sound at volume {intensity:F2}");

        // Example: Would call game audio system
        // AudioManager.PlayAmbient("rain_loop", intensity);
    }

    private void StopRainSound()
    {
        Context.Logger.LogInformation("Stopping rain sound");

        // Example: Would call game audio system
        // AudioManager.StopAmbient("rain_loop");
    }

    private void StartCreatingPuddles(float intensity)
    {
        ref var state = ref Context.GetState<RainState>();

        // Create puddles on walkable tiles over time
        // More intense rain = more puddles, faster

        int puddleCount = (int)(intensity * 20); // 0-20 puddles

        Context.Logger.LogInformation($"Creating {puddleCount} puddles");

        var random = new Random();

        for (int i = 0; i < puddleCount; i++)
        {
            // In real implementation, would check if tile is walkable
            // For now, just create random positions
            int x = random.Next(0, 100);
            int y = random.Next(0, 100);

            CreatePuddle(x, y);
        }
    }

    private void CreatePuddle(int x, int y)
    {
        ref var state = ref Context.GetState<RainState>();
        var position = (x, y);

        if (state.PuddlePositions.Contains(position))
        {
            return; // Puddle already exists
        }

        state.PuddlePositions.Add(position);

        // In real implementation, would:
        // 1. Check if tile at (x,y) is walkable
        // 2. Add puddle sprite/animation to tile
        // 3. Modify tile properties (slippery, splash effect)

        Context.Logger.LogInformation($"Puddle created at ({x}, {y})");

        // Example: Would call game map/tile system
        // MapManager.GetTile(x, y).AddEffect("puddle");
    }

    private void RemovePuddle(int x, int y)
    {
        ref var state = ref Context.GetState<RainState>();
        var position = (x, y);

        if (!state.PuddlePositions.Contains(position))
        {
            return;
        }

        state.PuddlePositions.Remove(position);

        Context.Logger.LogInformation($"Puddle removed at ({x}, {y})");

        // Example: Would call game map/tile system
        // MapManager.GetTile(x, y).RemoveEffect("puddle");
    }

    private void ClearAllPuddles()
    {
        ref var state = ref Context.GetState<RainState>();
        int puddleCount = state.PuddlePositions.Count;

        if (puddleCount == 0)
        {
            return;
        }

        Context.Logger.LogInformation($"Clearing {puddleCount} puddles");

        foreach (var position in state.PuddlePositions.ToList())
        {
            RemovePuddle(position.Item1, position.Item2);
        }

        state.PuddlePositions.Clear();
    }

    private void SchedulePuddleEvaporation(int seconds)
    {
        ref var state = ref Context.GetState<RainState>();

        Context.Logger.LogInformation($"Puddles will evaporate in {seconds} seconds");

        // Set up evaporation timer (handled in tick event)
        state.EvaporationDuration = seconds;
        state.EvaporationTimer = 0f;
        state.IsEvaporating = true;
    }

    /// <summary>
    /// Check if a tile position has a puddle.
    /// Other mods can call this to check for puddles.
    /// </summary>
    public bool HasPuddle(int x, int y)
    {
        if (!Context.HasState<RainState>())
        {
            return false;
        }
        ref var state = ref Context.GetState<RainState>();
        return state.PuddlePositions.Contains((x, y));
    }

    /// <summary>
    /// Get count of active puddles.
    /// </summary>
    public int GetPuddleCount()
    {
        if (!Context.HasState<RainState>())
        {
            return 0;
        }
        ref var state = ref Context.GetState<RainState>();
        return state.PuddlePositions.Count;
    }

    /// <summary>
    /// Get current rain intensity.
    /// </summary>
    public float GetRainIntensity()
    {
        if (!Context.HasState<RainState>())
        {
            return 0f;
        }
        ref var state = ref Context.GetState<RainState>();
        return state.RainIntensity;
    }

    /// <summary>
    /// Check if it's currently raining.
    /// </summary>
    public bool IsRaining()
    {
        if (!Context.HasState<RainState>())
        {
            return false;
        }
        ref var state = ref Context.GetState<RainState>();
        return state.IsRaining;
    }
}

// Component to store rain-specific state
public struct RainState
{
    public bool IsRaining;
    public float RainIntensity;
    public HashSet<(int, int)> PuddlePositions;
    public float EvaporationTimer;
    public float EvaporationDuration;
    public bool IsEvaporating;
}

// Instantiate and return the rain effects handler
return new RainEffects();
