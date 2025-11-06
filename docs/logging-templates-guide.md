# PokeSharp Logging Templates Guide
**Created:** November 5, 2025

Rich, visually appealing logging templates using Spectre.Console markup.

---

## 🎨 Available Templates

### Initialization Templates

#### LogSystemInitialized
```csharp
_logger.LogSystemInitialized("EntityFactoryService", ("templates", 42));
```
**Output:**  
`[green]✓[/] EntityFactoryService initialized [dim]| templates: 42[/]`

#### LogComponentInitialized
```csharp
_logger.LogComponentInitialized("AnimationLibrary", 8);
```
**Output:**  
`[green]✓[/] AnimationLibrary initialized with [cyan]8[/] items`

#### LogResourceLoaded
```csharp
_logger.LogResourceLoaded("Texture", "player", ("dimensions", "64x64px"), ("format", "Color"));
```
**Output:**  
`[green]✓[/] Loaded Texture '[cyan]player[/]' [dim]| dimensions: 64x64px, format: Color[/]`

---

### Entity Templates

#### LogEntitySpawned
```csharp
_logger.LogEntitySpawned("NPC", entityId, "npc/trainer", 10, 5);
```
**Output:**  
`[green]✓[/] Spawned [yellow]NPC[/] [dim]#123[/] from template '[cyan]npc/trainer[/]' at [magenta](10, 5)[/]`

#### LogEntityCreated
```csharp
_logger.LogEntityCreated("Player", entityId, 
    ("Position", "10,8"), ("Sprite", "player"), ("Camera", "attached"));
```
**Output:**  
`[green]✓[/] Created [yellow]Player[/] [dim]#42[/] [dim][Position, Sprite, Camera][/]`

---

### Asset Loading Templates

#### LogAssetLoadingStarted
```csharp
_logger.LogAssetLoadingStarted("tileset(s)", 3);
```
**Output:**  
`[blue]→[/] Loading [cyan]3[/] tileset(s)...`

#### LogAssetLoadedWithTiming
```csharp
_logger.LogAssetLoadedWithTiming("overworld", 45.5, 256, 256);
```
**Output:**  
`[green]✓[/] [cyan]overworld[/] [green]45.5ms[/] [dim](256x256px)[/]`  
(color changes to yellow if >100ms)

#### LogMapLoaded
```csharp
_logger.LogMapLoaded("test-map", 20, 15, 300, 6);
```
**Output:**  
`[green]✓[/] Map '[cyan]test-map[/]' loaded [dim]20x15[/] | [yellow]300[/] tiles, [magenta]6[/] objects`

---

### Performance Templates

#### LogFramePerformance
```csharp
_logger.LogFramePerformance(15.23f, 65.7f, 12.45f, 18.34f);
```
**Output:**  
`[blue]⚡[/] Performance: [cyan]15.2ms[/] [green]65.7 FPS[/] [dim]| Min: 12.5ms | Max: 18.3ms[/]`  
(FPS color: green ≥60, yellow ≥30, red <30)

#### LogSystemPerformance
```csharp
_logger.LogSystemPerformance("MovementSystem", 0.45, 1.23, 300);
```
**Output:**  
`[blue]│[/] [cyan]MovementSystem          [/] [green]  0.45ms[/] [dim]avg[/] [yellow]  1.23ms[/] [dim]max[/] [grey]│[/] [dim]300 calls[/]`  
(avg color: green <0.84ms, yellow <1.67ms, red >1.67ms)

#### LogMemoryStatistics
```csharp
_logger.LogMemoryStatistics(125.45, 234, 12, 1);
```
**Output:**  
`[blue]💾[/] Memory: [green]125.5MB[/] [dim]|[/] GC: [grey]G0:234[/] [grey]G1:12[/] [grey]G2:1[/]`  
(color: green <250MB, yellow <500MB, red >500MB)

---

### Warning Templates

#### LogSlowOperation
```csharp
_logger.LogSlowOperation("TextureLoad", 125.5, 100.0);
```
**Output:**  
`[yellow]⚠[/] Slow operation: [cyan]TextureLoad[/] took [red]125.5ms[/] [dim](threshold: 100.0ms)[/]`

#### LogResourceNotFound
```csharp
_logger.LogResourceNotFound("Template", "npc/invalid");
```
**Output:**  
`[yellow]⚠[/] Template '[red]npc/invalid[/]' not found, skipping`

#### LogOperationSkipped
```csharp
_logger.LogOperationSkipped("Object 'door'", "no template specified");
```
**Output:**  
`[yellow]⚠[/] Skipped: [cyan]Object 'door'[/] [dim](no template specified)[/]`

---

### Error Templates

#### LogOperationFailedWithRecovery
```csharp
_logger.LogOperationFailedWithRecovery("Load manifest", "Continuing with defaults");
```
**Output:**  
`[red]✗[/] Failed: [cyan]Load manifest[/] [dim]→[/] [yellow]Continuing with defaults[/]`

#### LogCriticalError
```csharp
_logger.LogCriticalError(ex, "Database connection");
```
**Output:**  
`[red bold]✗✗✗[/] CRITICAL: [cyan]Database connection[/] failed [dim]→[/] [red]SqlException: Connection timeout[/]`

---

### Progress Templates

#### LogBatchStarted
```csharp
_logger.LogBatchStarted("NPC Spawning", 6);
```
**Output:**  
`[blue]▶[/] Starting: [cyan]NPC Spawning[/] [dim](6 items)[/]`

#### LogBatchCompleted
```csharp
_logger.LogBatchCompleted("NPC Spawning", 5, 1, 125.5);
```
**Output:**  
`[green]✓[/] Completed: [cyan]NPC Spawning[/] [yellow]5 OK[/] [dim]1 failed[/] [grey]in 125.5ms[/]`  
(green if 0 failed, yellow if any failed)

---

### Input/Interaction Templates

#### LogControlsHint
```csharp
_logger.LogControlsHint("Use WASD to move");
```
**Output:**  
`[grey]🎮[/] [dim]Use WASD to move[/]`

#### LogZoomChanged
```csharp
_logger.LogZoomChanged("GBA (240x160)", 2.5f);
```
**Output:**  
`[blue]🔍[/] Zoom: [cyan]GBA (240x160)[/] [yellow]2.5x[/]`

---

### Diagnostic Templates

#### LogDiagnosticHeader
```csharp
_logger.LogDiagnosticHeader("ASSET MANAGER REPORT");
```
**Output:**
```
[blue bold]╔══════════════════════════════════════════╗[/]
[blue bold]║[/]  [cyan bold]ASSET MANAGER REPORT             [/]  [blue bold]║[/]
[blue bold]╚══════════════════════════════════════════╝[/]
```

#### LogDiagnosticInfo
```csharp
_logger.LogDiagnosticInfo("Total Textures", 15);
```
**Output:**  
`[grey]→[/] [cyan]Total Textures:[/] [yellow]15[/]`

#### LogDiagnosticSeparator
```csharp
_logger.LogDiagnosticSeparator();
```
**Output:**  
`[dim]═══════════════════════════════════════════[/]`

---

## 📋 Color Palette Reference

### Semantic Colors

| Use Case | Color | Markup |
|----------|-------|--------|
| Success | Green | `[green]✓[/]` |
| Warning | Yellow | `[yellow]⚠[/]` |
| Error | Red | `[red]✗[/]` |
| Critical | Red Bold | `[red bold]✗✗✗[/]` |
| Info | Blue | `[blue]→[/]` |
| Progress | Blue | `[blue]▶[/]` |

### Data Colors

| Data Type | Color | Example |
|-----------|-------|---------|
| Names/IDs | Cyan | `[cyan]player[/]` |
| Counts | Cyan/Yellow | `[cyan]42[/]` |
| Coordinates | Magenta | `[magenta](10, 5)[/]` |
| Entity Types | Yellow | `[yellow]NPC[/]` |
| Details | Grey/Dim | `[dim]details[/]` |
| Metadata | Grey | `[grey]info[/]` |

### Icons

| Icon | Meaning | Markup |
|------|---------|--------|
| ✓ | Success | `[green]✓[/]` |
| ✗ | Failure | `[red]✗[/]` |
| ⚠ | Warning | `[yellow]⚠[/]` |
| → | Loading/Info | `[blue]→[/]` |
| ▶ | Started | `[blue]▶[/]` |
| ⚡ | Performance | `[blue]⚡[/]` |
| 💾 | Memory | `[blue]💾[/]` |
| 🔍 | Zoom/View | `[blue]🔍[/]` |
| 🎮 | Controls/Input | `[grey]🎮[/]` |

---

## 💡 Usage Examples

### Initialization
```csharp
// System init with details
_logger.LogSystemInitialized("PhysicsEngine", 
    ("threads", 4), ("timestep", "1/60"));

// Component init with count
_logger.LogComponentInitialized("ParticlePool", 1000);

// Resource loaded with metadata
_logger.LogResourceLoaded("Shader", "main.fx", 
    ("version", "5.0"), ("compiled", "true"));
```

### Entity Management
```csharp
// Spawn entity
_logger.LogEntitySpawned("Enemy", entity.Id, "enemy/goblin", 15, 20);

// Create entity with components
_logger.LogEntityCreated("Projectile", entity.Id,
    ("Position", "5,10"), ("Velocity", "fast"), ("Damage", "25"));
```

### Asset Loading
```csharp
// Start batch
_logger.LogAssetLoadingStarted("textures", 50);

// Individual asset with timing
_logger.LogAssetLoadedWithTiming("background", 78.5, 1920, 1080);

// Complete batch
_logger.LogBatchCompleted("Texture Loading", 48, 2, 1250.5);
```

### Performance
```csharp
// Frame stats
_logger.LogFramePerformance(14.2f, 70.4f, 11.5f, 22.3f);

// System stats
_logger.LogSystemPerformance("RenderSystem", 2.34, 5.67, 300);

// Memory stats
_logger.LogMemoryStatistics(156.8, 450, 23, 2);
```

### Warnings & Errors
```csharp
// Slow operation
_logger.LogSlowOperation("DatabaseQuery", 250.5, 100.0);

// Resource not found
_logger.LogResourceNotFound("Texture", "missing.png");

// Operation failed with recovery
_logger.LogOperationFailedWithRecovery("Save game", "Using auto-save");

// Critical error
_logger.LogCriticalError(ex, "Network connection");
```

### Diagnostics
```csharp
_logger.LogDiagnosticHeader("SYSTEM STATUS REPORT");
_logger.LogDiagnosticInfo("Entities Active", 1234);
_logger.LogDiagnosticInfo("Systems Running", 9);
_logger.LogDiagnosticInfo("Memory Usage", "125MB");
_logger.LogDiagnosticSeparator();
```

---

## 🎯 Visual Output Example

When the game runs, you'll see beautifully formatted output like:

```
[grey][10:23:45.123][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [green]✓[/] Loaded Manifest '[cyan]Assets/manifest.json[/]'
[grey][10:23:45.234][/] [green][INFO ][/] [dim][AssetManifest][/] [blue bold]AssetManager[/]: [blue]→[/] Loading [cyan]3[/] tileset(s)...
[grey][10:23:45.345][/] [grey][DEBUG][/] [blue bold]AssetManager[/]: [green]✓[/] [cyan]overworld[/] [green]45.2ms[/] [dim](256x256px)[/]
[grey][10:23:45.456][/] [green][INFO ][/] [blue bold]AssetManager[/]: [green]✓[/] Loaded [cyan]3[/] tilesets
[grey][10:23:45.567][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [green]✓[/] EntityFactoryService initialized [dim]| templates: 42[/]
[grey][10:23:45.678][/] [green][INFO ][/] [dim][Loading:test-map][/] [purple bold]MapLoader[/]: [green]✓[/] Map '[cyan]test-map[/]' loaded [dim]20x15[/] | [yellow]300[/] tiles, [magenta]6[/] objects
[grey][10:23:45.789][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [green]✓[/] Created [yellow]Player[/] [dim]#1[/] [dim][Position, Sprite, GridMovement, Camera][/]
[grey][10:23:45.890][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [grey]🎮[/] [dim]Use WASD or Arrow Keys to move![/]
[grey][10:23:45.991][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [blue]▶[/] Starting: [cyan]NPC Spawning[/] [dim](6 items)[/]
[grey][10:23:46.012][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [green]✓[/] Spawned [yellow]NPC[/] [dim]#2[/] from template '[cyan]npc/generic[/]' at [magenta](15, 8)[/]

... 5 seconds later ...

[grey][10:23:50.567][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [blue]⚡[/] Performance: [cyan]15.2ms[/] [green]65.7 FPS[/] [dim]| Min: 12.5ms | Max: 18.3ms[/]
[grey][10:23:50.678][/] [green][INFO ][/] [cyan1 bold]PokeSharpGame[/]: [blue]💾[/] Memory: [green]125.5MB[/] [dim]|[/] GC: [grey]G0:234[/] [grey]G1:12[/] [grey]G2:1[/]
[grey][10:23:50.789][/] [green][INFO ][/] [lime bold]SystemManager[/]: [blue]│[/] [cyan]SpatialHashSystem        [/] [green]  0.12ms[/] [dim]avg[/] [yellow]  0.45ms[/] [dim]max[/] [grey]│[/] [dim]300 calls[/]
[grey][10:23:50.890][/] [green][INFO ][/] [lime bold]SystemManager[/]: [blue]│[/] [cyan]MovementSystem           [/] [green]  0.45ms[/] [dim]avg[/] [yellow]  1.23ms[/] [dim]max[/] [grey]│[/] [dim]300 calls[/]
[grey][10:23:50.991][/] [green][INFO ][/] [lime bold]SystemManager[/]: [blue]│[/] [cyan]ZOrderRenderSystem       [/] [yellow]  2.34ms[/] [dim]avg[/] [yellow]  3.45ms[/] [dim]max[/] [grey]│[/] [dim]300 calls[/]
```

---

## 🎨 Visual Features

### Icons & Symbols
- **✓** - Success operations (green)
- **✗** - Failed operations (red)
- **✗✗✗** - Critical errors (red bold)
- **⚠** - Warnings (yellow)
- **→** - Information/Loading (blue)
- **▶** - Started operations (blue)
- **│** - Table borders (blue/grey)
- **⚡** - Performance metrics (blue)
- **💾** - Memory info (blue)
- **🔍** - Zoom/view changes (blue)
- **🎮** - Input controls (grey)

### Color Coding
- **Green** - Success, good performance
- **Yellow** - Warning, moderate performance
- **Red** - Error, poor performance
- **Cyan** - Names, IDs, important values
- **Magenta** - Coordinates, positions
- **Grey** - Metadata, less important info
- **Dim** - Supporting information
- **Bold** - Emphasis on categories and errors

### Hierarchical Display
- **Main operation** in bold color
- **Details** in dim/grey
- **Success/failure** with visual indicators
- **Nested scopes** shown in brackets with dim color

---

## 🚀 Benefits

1. **Visual Consistency** - All similar operations look the same
2. **Quick Scanning** - Icons and colors make info jump out
3. **Information Density** - Compact yet readable
4. **Performance Aware** - Colors change based on metrics
5. **Error Visibility** - Problems stand out immediately
6. **Beautiful Output** - Professional appearance

---

## 📝 Best Practices

1. **Use templates for recurring patterns** - Don't manually format
2. **Let colors convey meaning** - Green=good, Yellow=warning, Red=error
3. **Keep icons consistent** - Same icon for same operation type
4. **Provide context in dim** - Details that support main message
5. **Highlight important data** - Use cyan for IDs, yellow for counts
6. **Table alignment** - Performance stats use fixed-width formatting

The template system makes logging beautiful, consistent, and informative! 🎉



