# Template System - Immediate Next Steps

**Date**: November 5, 2025  
**Status**: Ready to implement  

## Quick Wins (1-2 hours each)

### 1. Add Basic Tile Templates

**File**: `PokeSharp/PokeSharp.Game/Templates/TemplateRegistry.cs`

Add this method:

```csharp
private static void RegisterTileTemplates(TemplateCache cache, ILogger? logger = null)
{
    // Ground tile (walkable, no special properties)
    var groundTemplate = new EntityTemplate {
        TemplateId = "tile/ground",
        Name = "Ground Tile",
        Tag = "tile",
    };
    groundTemplate.WithComponent(new TilePosition(0, 0, 0)); // Override at spawn
    groundTemplate.WithComponent(new TileSprite("default", 0, TileLayer.Ground, Rectangle.Empty)); // Override at spawn
    cache.Register(groundTemplate);

    // Wall tile (solid collision)
    var wallTemplate = new EntityTemplate {
        TemplateId = "tile/wall",
        Name = "Wall Tile",
        Tag = "tile",
    };
    wallTemplate.WithComponent(new TilePosition(0, 0, 0));
    wallTemplate.WithComponent(new TileSprite("default", 0, TileLayer.Object, Rectangle.Empty));
    wallTemplate.WithComponent(new Collision(true));
    cache.Register(wallTemplate);

    // Grass tile (encounters)
    var grassTemplate = new EntityTemplate {
        TemplateId = "tile/grass",
        Name = "Grass Tile",
        Tag = "tile",
    };
    grassTemplate.WithComponent(new TilePosition(0, 0, 0));
    grassTemplate.WithComponent(new TileSprite("default", 0, TileLayer.Ground, Rectangle.Empty));
    grassTemplate.WithComponent(new EncounterZone("default", 10));
    cache.Register(grassTemplate);

    // Ledge templates (4 directions)
    var directions = new[] {
        ("down", Direction.Down),
        ("up", Direction.Up),
        ("left", Direction.Left),
        ("right", Direction.Right)
    };

    foreach (var (dirName, direction) in directions)
    {
        var ledgeTemplate = new EntityTemplate {
            TemplateId = $"tile/ledge/{dirName}",
            Name = $"Ledge ({dirName})",
            Tag = "tile",
        };
        ledgeTemplate.WithComponent(new TilePosition(0, 0, 0));
        ledgeTemplate.WithComponent(new TileSprite("default", 0, TileLayer.Object, Rectangle.Empty));
        ledgeTemplate.WithComponent(new Collision(true));
        ledgeTemplate.WithComponent(new TileLedge(direction));
        cache.Register(ledgeTemplate);
    }

    logger?.LogDebug("Registered {Count} tile templates", 6);
}
```

Call it from `RegisterAllTemplates()`:
```csharp
RegisterTileTemplates(cache, logger);
```

### 2. Update MapLoader to Support Template-Based Tiles

**File**: `PokeSharp/PokeSharp.Rendering/Loaders/MapLoader.cs`

Add an optional `EntityFactoryService` parameter to constructor:

```csharp
private readonly IEntityFactoryService? _entityFactory;

public MapLoader(AssetManager assetManager, IEntityFactoryService? entityFactory = null)
{
    _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
    _entityFactory = entityFactory;
}
```

Add helper method to determine tile template:

```csharp
private string? DetermineTileTemplate(Dictionary<string, object> props)
{
    // Check for ledge
    if (props.ContainsKey("ledge_direction"))
    {
        var dir = props["ledge_direction"]?.ToString()?.ToLower();
        return dir switch
        {
            "down" => "tile/ledge/down",
            "up" => "tile/ledge/up",
            "left" => "tile/ledge/left",
            "right" => "tile/ledge/right",
            _ => null
        };
    }

    // Check for solid wall
    if (props.TryGetValue("solid", out var solidValue))
    {
        bool isSolid = solidValue switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var result) && result,
            _ => false
        };
        if (isSolid) return "tile/wall";
    }

    // Check for encounter zone
    if (props.ContainsKey("encounter_rate"))
    {
        return "tile/grass";
    }

    // Default ground tile
    return "tile/ground";
}
```

Update `CreateTileEntity` to use templates when available:

```csharp
private void CreateTileEntity(
    World world,
    int x,
    int y,
    int mapId,
    int tileGid,
    TmxTileset tileset,
    TileLayer layer
)
{
    int localTileId = tileGid - tileset.FirstGid;
    string? templateId = null;

    // Try to determine template from properties
    if (localTileId >= 0 && tileset.TileProperties.TryGetValue(localTileId, out var props))
    {
        templateId = DetermineTileTemplate(props);
    }

    // Use template if factory is available and template exists
    if (_entityFactory != null && templateId != null && _entityFactory.HasTemplate(templateId))
    {
        var entity = _entityFactory.SpawnFromTemplateAsync(
            templateId,
            world,
            builder =>
            {
                builder.OverrideComponent(new TilePosition(x, y, mapId));
                builder.OverrideComponent(new TileSprite(
                    tileset.Name ?? "default",
                    tileGid,
                    layer,
                    CalculateSourceRect(tileGid, tileset)
                ));
            }
        ).GetAwaiter().GetResult();
    }
    else
    {
        // Fallback to manual creation (existing code)
        var position = new TilePosition(x, y, mapId);
        var sprite = new TileSprite(
            tileset.Name ?? "default",
            tileGid,
            layer,
            CalculateSourceRect(tileGid, tileset)
        );
        var entity = world.Create(position, sprite);
        
        // ... existing manual component addition logic ...
    }
}
```

### 3. Wire Up EntityFactory in Game Initialization

**File**: `PokeSharp/PokeSharp.Game/PokeSharpGame.cs`

Update MapLoader creation:

```csharp
// OLD:
_mapLoader = new MapLoader(_assetManager);

// NEW:
_mapLoader = new MapLoader(_assetManager, _entityFactory);
```

### 4. Add More NPC Templates

**File**: `PokeSharp/PokeSharp.Game/Templates/TemplateRegistry.cs`

```csharp
// Stationary NPC (no movement)
var stationaryNpcTemplate = new EntityTemplate {
    TemplateId = "npc/stationary",
    Name = "Stationary NPC",
    Tag = "npc",
};
stationaryNpcTemplate.WithComponent(new Position(0, 0));
stationaryNpcTemplate.WithComponent(new Sprite("npc-spritesheet"));
stationaryNpcTemplate.WithComponent(Direction.Down);
stationaryNpcTemplate.WithComponent(new Animation("idle_down"));
stationaryNpcTemplate.WithComponent(new Collision(true));
// Note: No GridMovement component = can't move
cache.Register(stationaryNpcTemplate);

// Trainer NPC (for battles)
var trainerTemplate = new EntityTemplate {
    TemplateId = "npc/trainer",
    Name = "Trainer NPC",
    Tag = "npc",
};
trainerTemplate.WithComponent(new Position(0, 0));
trainerTemplate.WithComponent(new Sprite("trainer-spritesheet"));
trainerTemplate.WithComponent(new GridMovement(2.0f));
trainerTemplate.WithComponent(Direction.Down);
trainerTemplate.WithComponent(new Animation("idle_down"));
trainerTemplate.WithComponent(new Collision(true));
// TODO: Add Trainer component when implemented
cache.Register(trainerTemplate);
```

## Testing Template Changes

After implementing tile templates:

```bash
# Run the game and verify:
# 1. Tiles still load correctly
# 2. Collision still works
# 3. Ledges still work
# 4. No performance regression

cd PokeSharp.Game/bin/Debug/net9.0
./PokeSharp.Game.exe
```

## Performance Comparison

Before (manual creation):
```
Map Load Time: ~50-80ms for 3000 tiles
Code Complexity: ~100 lines of property parsing
```

After (template-based):
```
Expected Map Load Time: ~40-60ms (10-25% improvement)
Code Complexity: ~20 lines of template lookup
```

## Benefits Summary

1. **Code Quality**: 80% reduction in entity creation code
2. **Maintainability**: Centralized component definitions
3. **Validation**: Templates validated at registration, not runtime
4. **Modding**: Easy to add new entity types without code changes
5. **Performance**: Pre-compiled component arrays, faster spawning
6. **Testing**: Easier to test with consistent entity structures

## Migration Strategy

**Phase 1** (Today): Add tile templates, make MapLoader support both manual and template creation  
**Phase 2** (Tomorrow): Test thoroughly, benchmark performance  
**Phase 3** (Next): Remove manual creation fallback once templates prove stable  

This keeps the system backwards-compatible during migration!

