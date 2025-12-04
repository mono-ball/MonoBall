# Weather System Mod

A comprehensive weather system mod for MonoBall Framework demonstrating custom event creation, inter-script communication, and mod extensibility.

## Features

### 🌦️ Dynamic Weather Changes
- Automatic weather transitions every 5 minutes (configurable)
- Seasonal weather patterns (snow in winter, sunshine in summer)
- Natural weather progression with smooth transitions
- Multiple weather types: Rain, Thunder, Snow, Sunshine, Clear, Fog

### ⚡ Thunder & Lightning
- Dramatic lightning strikes during thunderstorms
- Visual lightning flash effects with screen shake
- Thunder sound effects with realistic distance delay
- Optional damage to entities in open areas
- Environmental effects (fires, scared wild Pokémon)
- Scorch marks at strike locations

### 🌧️ Rain Effects
- Particle-based rain visual effects
- Ambient rain sound (intensity-based volume)
- Dynamic puddle creation on walkable tiles
- Gradual puddle evaporation over time
- Accelerated evaporation in sunshine

### 🐾 Weather-Based Encounters
- Water-type Pokémon spawn more in rain (1.5x)
- Electric-type Pokémon surge in thunderstorms (2.25x)
- Ice-type Pokémon appear in snow (2.25x)
- Fire and Grass-types thrive in sunshine (1.4x)
- Dynamic encounter rate adjustments
- Type-specific multipliers for realistic spawns

## Installation

1. Copy the `weather-system` folder to your `Mods/examples/` directory:
   ```
   MonoBall Framework/Mods/examples/weather-system/
   ```

2. The mod will be automatically loaded by the MonoBall Framework modding engine.

3. Verify installation by checking the logs for:
   ```
   Weather Controller initialized
   Rain Effects system initialized
   Thunder Effects system initialized
   Weather Encounters system initialized
   ```

## Configuration

Edit `mod.json` to customize weather behavior:

```json
{
  "configuration": {
    "weatherChangeDurationMinutes": 5,       // How often weather changes
    "thunderProbabilityDuringRain": 0.3,    // 30% chance of thunder
    "snowProbabilityInWinter": 0.6,         // 60% chance of snow in winter
    "enableWeatherDamage": true,             // Thunder can damage entities
    "weatherEncounterMultiplier": 1.5        // Base encounter rate multiplier
  }
}
```

## Custom Events

This mod defines the following custom events that other mods can subscribe to:

### WeatherEventBase
Base class for all weather events with common properties:
- `WeatherType` - The weather type
- `Intensity` - Effect intensity (0.0-1.0)
- `DurationSeconds` - Expected duration

### RainStartedEvent
```csharp
EventBus.Subscribe<RainStartedEvent>(evt => {
    // React to rain starting
    bool createsPuddles = evt.CreatePuddles;
    bool canThunder = evt.CanThunder;
    float intensity = evt.Intensity;
});
```

### RainStoppedEvent
```csharp
EventBus.Subscribe<RainStoppedEvent>(evt => {
    // React to rain stopping
    bool puddlesPersist = evt.PersistPuddles;
    int evaporationTime = evt.PuddleEvaporationSeconds;
});
```

### ThunderstrikeEvent
```csharp
EventBus.Subscribe<ThunderstrikeEvent>(evt => {
    // React to lightning strikes
    var position = evt.StrikePosition;
    int damage = evt.Damage;
    int radius = evt.AffectRadius;
});
```

### SnowStartedEvent
```csharp
EventBus.Subscribe<SnowStartedEvent>(evt => {
    // React to snow starting
    bool accumulates = evt.AccumulatesOnGround;
    float rate = evt.AccumulationRate;
    int maxDepth = evt.MaxDepthLayers;
    bool icy = evt.CreatesIcyTerrain;
});
```

### SunshineEvent
```csharp
EventBus.Subscribe<SunshineEvent>(evt => {
    // React to sunshine
    float brightness = evt.BrightnessMultiplier;
    bool evaporates = evt.AcceleratesEvaporation;
    bool boostsGrass = evt.BoostsGrassTypes;
});
```

### WeatherChangedEvent
```csharp
EventBus.Subscribe<WeatherChangedEvent>(evt => {
    // React to any weather change
    string? previous = evt.PreviousWeather;
    string current = evt.NewWeather;
    bool natural = evt.IsNaturalTransition;
});
```

## Extending This Mod

### Adding New Weather Types

1. Define a new event in `events/WeatherEvents.csx`:
```csharp
public record FogStartedEvent : WeatherEventBase
{
    public float VisibilityRange { get; init; } = 10.0f;
    public bool ReducesSpawnRates { get; init; } = true;
}
```

2. Update `weather_controller.csx` to publish your event:
```csharp
case "Fog":
    PublishWeatherEvent(new FogStartedEvent
    {
        WeatherType = "Fog",
        Intensity = intensity,
        VisibilityRange = 10.0f * (1.0f - intensity)
    });
    break;
```

3. Create a new script (e.g., `fog_effects.csx`) to handle the event:
```csharp
EventBus?.Subscribe<FogStartedEvent>(OnFogStarted);
```

### Creating Dependent Mods

Other mods can subscribe to weather events without modifying this mod:

```csharp
#r "MonoBall Framework.Engine.Core.dll"
#load "../weather-system/events/WeatherEvents.csx"

using MonoBall Framework.Engine.Core.Events;

public class MyWeatherMod : ScriptBase
{
    public override async Task OnInitializedAsync()
    {
        // Subscribe to weather events
        EventBus?.Subscribe<RainStartedEvent>(OnRain);
        EventBus?.Subscribe<ThunderstrikeEvent>(OnThunder);
    }

    private void OnRain(RainStartedEvent evt)
    {
        // Your custom rain logic
    }
}
```

### Integration Examples

#### Plant Growth Mod
```csharp
// Plants grow faster in rain and sunshine
EventBus?.Subscribe<RainStartedEvent>(evt => {
    plantGrowthRate *= 1.5f;
});

EventBus?.Subscribe<SunshineEvent>(evt => {
    plantGrowthRate *= evt.BrightnessMultiplier;
});
```

#### Energy System Mod
```csharp
// Solar panels produce more energy in sunshine
EventBus?.Subscribe<SunshineEvent>(evt => {
    solarPanelOutput *= evt.BrightnessMultiplier;
});

// Wind turbines work better in storms
EventBus?.Subscribe<RainStartedEvent>(evt => {
    if (evt.CanThunder) {
        windTurbineOutput *= 1.8f;
    }
});
```

#### Quest System Integration
```csharp
// Weather-dependent quests
EventBus?.Subscribe<ThunderstrikeEvent>(evt => {
    if (questActive && questType == "CatchElectricType") {
        questProgress++;
    }
});
```

## Script Architecture

### weather_controller.csx
**Central weather management**
- Manages weather state and transitions
- Publishes weather events
- Configurable timing and probabilities
- Manual weather control API

### rain_effects.csx
**Visual and audio rain effects**
- Rain particle system
- Puddle creation and management
- Ambient sound effects
- Evaporation system

### thunder_effects.csx
**Lightning and thunder**
- Lightning flash visuals
- Thunder sound with distance delay
- Damage application
- Environmental effects (fires)
- Scorch marks

### weather_encounters.csx
**Dynamic Pokémon spawns**
- Type-specific encounter multipliers
- Real-time spawn rate adjustments
- Weather-appropriate spawns
- Query API for other mods

## API Reference

### WeatherController

```csharp
// Manually change weather
controller.SetWeather("Rain", durationSeconds: 300);

// Get current weather
string weather = controller.GetCurrentWeather();
```

### RainEffects

```csharp
// Check for puddles
bool hasPuddle = rainEffects.HasPuddle(x, y);

// Get rain intensity
float intensity = rainEffects.GetRainIntensity();

// Check if raining
bool isRaining = rainEffects.IsRaining();
```

### ThunderEffects

```csharp
// Manual thunderstrike (for items/abilities)
thunderEffects.TriggerThunderstrike(x, y, damage: 15);

// Get strike count
int strikes = thunderEffects.GetThunderstrikeCount();
```

### WeatherEncounters

```csharp
// Get type multiplier
float waterMultiplier = encounters.GetTypeMultiplier("Water");

// Calculate effective spawn chance
float chance = encounters.CalculateSpawnChance("Electric", 0.1f);

// Manual multiplier override
encounters.SetTypeMultiplier("Dragon", 2.0f);
```

## Performance Considerations

- Weather changes are event-driven (no polling)
- Puddle creation is batched
- Visual effects scale with intensity
- Thunder strikes are independent events
- Encounter multipliers cached until weather change

## Debugging

Enable detailed logging in each script:
- Look for "Weather Controller initialized" messages
- Check for event subscription confirmations
- Monitor weather change logs
- Track encounter multiplier applications

## Future Enhancements

Potential additions:
- [ ] Hail weather with damage over time
- [ ] Sandstorm weather affecting visibility
- [ ] Fog reducing encounter distance
- [ ] Weather-based move power changes (Rain Dance, Sunny Day)
- [ ] Weather patterns (fronts, storms moving across map)
- [ ] Climate zones with different weather probabilities
- [ ] Weather forecast system
- [ ] Weather-dependent NPC dialogue

## Contributing

To contribute improvements:
1. Fork the MonoBall Framework repository
2. Create a branch for your weather enhancement
3. Submit a pull request with your changes
4. Update this README with new features

## License

Part of the MonoBall Framework project. See main project license.

## Credits

Created by the MonoBall Framework Team as an example mod demonstrating:
- Custom event creation with IGameEvent
- Event-driven architecture
- Inter-script communication via EventBus
- State management in mods
- Configuration system usage
- Mod composition and extensibility

This mod serves as a reference implementation for building complex, event-driven mods in MonoBall Framework.
