#r "MonoBallFramework.Engine.Core.dll"

using MonoBallFramework.Engine.Core.Events;

/// <summary>
/// Base record for all weather-related events.
/// Provides common properties shared across weather changes.
/// </summary>
public abstract record WeatherEventBase : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The weather type that is starting or has significance in this event.
    /// </summary>
    public required string WeatherType { get; init; }

    /// <summary>
    /// Intensity of the weather effect (0.0 to 1.0).
    /// Used for scaling visual effects and gameplay impact.
    /// </summary>
    public float Intensity { get; init; } = 0.5f;

    /// <summary>
    /// Expected duration of the weather in seconds.
    /// -1 indicates indefinite duration.
    /// </summary>
    public int DurationSeconds { get; init; } = -1;
}

/// <summary>
/// Event fired when rain weather begins.
/// Triggers rain visual effects, sound effects, and encounter modifications.
/// </summary>
public record RainStartedEvent : WeatherEventBase
{
    /// <summary>
    /// Whether puddles should form on tiles during this rain.
    /// </summary>
    public bool CreatePuddles { get; init; } = true;

    /// <summary>
    /// Whether this rain can transition to thunderstorm.
    /// </summary>
    public bool CanThunder { get; init; } = true;
}

/// <summary>
/// Event fired when rain weather stops.
/// Triggers cleanup of rain effects and puddle evaporation.
/// </summary>
public record RainStoppedEvent : WeatherEventBase
{
    /// <summary>
    /// Whether puddles should persist after rain stops.
    /// </summary>
    public bool PersistPuddles { get; init; } = true;

    /// <summary>
    /// How long puddles should take to evaporate (seconds).
    /// </summary>
    public int PuddleEvaporationSeconds { get; init; } = 120;
}

/// <summary>
/// Event fired when a thunderstrike occurs during thunderstorm weather.
/// Can cause damage to entities in open areas and trigger special effects.
/// </summary>
public record ThunderstrikeEvent : WeatherEventBase
{
    /// <summary>
    /// The map coordinates where the lightning struck.
    /// </summary>
    public required (int X, int Y) StrikePosition { get; init; }

    /// <summary>
    /// Damage dealt to entities at strike position.
    /// 0 means this is a visual-only strike.
    /// </summary>
    public int Damage { get; init; } = 0;

    /// <summary>
    /// Radius in tiles affected by the strike.
    /// </summary>
    public int AffectRadius { get; init; } = 2;

    /// <summary>
    /// Whether this strike can cause fires or other environmental effects.
    /// </summary>
    public bool CausesEnvironmentalEffects { get; init; } = false;
}

/// <summary>
/// Event fired when snow weather begins.
/// Triggers snow visual effects, tile coverage, and encounter modifications.
/// </summary>
public record SnowStartedEvent : WeatherEventBase
{
    /// <summary>
    /// Whether snow should accumulate on ground tiles.
    /// </summary>
    public bool AccumulatesOnGround { get; init; } = true;

    /// <summary>
    /// Rate of snow accumulation (tiles per minute).
    /// </summary>
    public float AccumulationRate { get; init; } = 0.5f;

    /// <summary>
    /// Maximum depth of snow accumulation in layers.
    /// </summary>
    public int MaxDepthLayers { get; init; } = 3;

    /// <summary>
    /// Whether the snow creates slippery surfaces.
    /// </summary>
    public bool CreatesIcyTerrain { get; init; } = false;
}

/// <summary>
/// Event fired when sunny/clear weather begins.
/// Triggers sunshine effects and clears weather-based modifiers.
/// </summary>
public record SunshineEvent : WeatherEventBase
{
    /// <summary>
    /// Brightness multiplier for lighting (1.0 = normal, >1.0 = brighter).
    /// </summary>
    public float BrightnessMultiplier { get; init; } = 1.2f;

    /// <summary>
    /// Whether this sunshine event should accelerate puddle evaporation.
    /// </summary>
    public bool AcceleratesEvaporation { get; init; } = true;

    /// <summary>
    /// Whether this sunshine boosts grass-type Pokémon spawns.
    /// </summary>
    public bool BoostsGrassTypes { get; init; } = true;

    /// <summary>
    /// Temperature increase effect (affects mechanics like ice melting).
    /// </summary>
    public float TemperatureBonus { get; init; } = 5.0f;
}

/// <summary>
/// Event fired when weather changes from one type to another.
/// Allows mods to react to weather transitions.
/// </summary>
public record WeatherChangedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The previous weather type (null if this is the first weather).
    /// </summary>
    public string? PreviousWeather { get; init; }

    /// <summary>
    /// The new weather type that has started.
    /// </summary>
    public required string NewWeather { get; init; }

    /// <summary>
    /// Whether this is a natural weather transition or forced by script/item.
    /// </summary>
    public bool IsNaturalTransition { get; init; } = true;
}

/// <summary>
/// Event fired when weather is cleared/reset to no weather.
/// </summary>
public record WeatherClearedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The weather type that was cleared.
    /// </summary>
    public required string ClearedWeather { get; init; }

    /// <summary>
    /// Whether effects should be cleaned up immediately or fade out.
    /// </summary>
    public bool ImmediateCleanup { get; init; } = false;
}
