# Component Pooling System

## Overview

The Component Pooling System reduces GC pressure for frequently accessed components in the Arch ECS architecture. While
Arch manages component storage internally through archetypes, this system provides pooling for temporary component
operations and complex initialization.

## Performance Impact

**Expected Benefits:**

- **15-25% reduction** in component-related allocations
- **10-15% reduction** in overall GC pressure
- Particularly effective for components with reference types (Animation's HashSet)
- Most impactful during intense gameplay (many entities moving/animating)

**Targeted Components:**

- `Position` (~7 accesses/frame): Movement, collision, rendering
- `GridMovement` (~8 accesses/frame): Movement calculations
- `Sprite`: Rendering updates
- `Animation`: Frame updates and state management

## Architecture

### ComponentPool<T>

Generic pool for individual component types. Thread-safe using `ConcurrentBag<T>`.

```csharp
// Create a pool
var pool = new ComponentPool<Position>(maxSize: 1000);

// Rent from pool (creates new if empty)
var position = pool.Rent();

// Use for temporary calculations
position.X = targetX;
position.Y = targetY;

// Return to pool (resets to default)
pool.Return(position);

// Check statistics
var stats = pool.GetStatistics();
Console.WriteLine($"Reuse rate: {stats.ReuseRate:P1}");
```

### ComponentPoolManager

Centralized manager for all component pools. Pre-configured for high-frequency components.

```csharp
// Initialize (typically in startup)
var poolManager = new ComponentPoolManager(logger, enableStatistics: true);

// Quick access to common pools
var position = poolManager.RentPosition();
// ... use position ...
poolManager.ReturnPosition(position);

// Access any pool
var customPool = poolManager.GetPool<MyComponent>(maxSize: 500);

// Generate performance report
var report = poolManager.GenerateReport();
logger.LogInformation(report);
```

## Integration Examples

### 1. Dependency Injection (Recommended)

```csharp
// In startup/configuration
serviceContainer
    .AddEntityPooling()  // Existing entity pooling
    .AddComponentPooling(enableStatistics: true);  // New component pooling

// In systems
public class MySystem : ISystem
{
    private readonly ComponentPoolManager _componentPools;

    public MySystem(ComponentPoolManager componentPools)
    {
        _componentPools = componentPools;
    }

    public void Update(GameTime gameTime)
    {
        // Use pools for temporary calculations
        var tempPos = _componentPools.RentPosition();
        try
        {
            // Perform calculations
            tempPos.X = targetX;
            tempPos.Y = targetY;
            // ... use tempPos ...
        }
        finally
        {
            _componentPools.ReturnPosition(tempPos);
        }
    }
}
```

### 2. Movement System Integration

```csharp
public class MovementSystem : BaseSystem
{
    private readonly ComponentPoolManager _componentPools;

    protected override void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Query entities with movement components
        var query = new QueryDescription().WithAll<Position, GridMovement>();

        World.Query(in query, (Entity entity, ref Position pos, ref GridMovement movement) =>
        {
            if (!movement.IsMoving) return;

            // Rent temporary position for calculations
            var newPos = _componentPools.RentPosition();

            try
            {
                // Calculate new position
                newPos.X = pos.X + deltaX;
                newPos.Y = pos.Y + deltaY;
                newPos.PixelX = Lerp(movement.StartPosition.X, movement.TargetPosition.X, progress);
                newPos.PixelY = Lerp(movement.StartPosition.Y, movement.TargetPosition.Y, progress);

                // Apply if valid
                if (IsValidPosition(newPos))
                {
                    pos = newPos;
                }
            }
            finally
            {
                _componentPools.ReturnPosition(newPos);
            }
        });
    }
}
```

### 3. Animation System Integration

```csharp
public class AnimationSystem : BaseSystem
{
    private readonly ComponentPoolManager _componentPools;

    protected override void Update(GameTime gameTime)
    {
        var query = new QueryDescription().WithAll<Animation, Sprite>();

        World.Query(in query, (ref Animation anim, ref Sprite sprite) =>
        {
            // Rent temporary animation for state calculations
            var tempAnim = _componentPools.RentAnimation();

            try
            {
                // Copy current state
                tempAnim = anim;

                // Update frame timing
                tempAnim.FrameTimer += deltaTime;

                if (tempAnim.FrameTimer >= frameDuration)
                {
                    tempAnim.FrameTimer = 0;
                    tempAnim.CurrentFrame = (tempAnim.CurrentFrame + 1) % frameCount;
                }

                // Apply changes
                anim = tempAnim;
            }
            finally
            {
                _componentPools.ReturnAnimation(tempAnim);
            }
        });
    }
}
```

## When to Use Component Pools

### ✅ Good Use Cases

1. **Temporary calculations**: Position interpolation, collision checks
2. **State transitions**: Animation state copying, movement state changes
3. **Batch operations**: Processing multiple components of same type
4. **High-frequency updates**: Components accessed every frame
5. **Complex initialization**: Components with reference types (Animation's HashSet)

### ❌ When NOT to Use

1. **Direct ECS operations**: Let Arch handle component storage
2. **Long-lived components**: Just use entity.Get<T>() and entity.Set<T>()
3. **Rarely accessed components**: Pooling overhead not worth it
4. **Simple value types**: int, float, bool (too cheap to pool)

## Monitoring and Optimization

### Statistics Tracking

```csharp
// Log statistics periodically (e.g., every 60 seconds)
componentPoolManager.LogStatistics();

// Get specific pool stats
var positionStats = componentPoolManager.GetPool<Position>().GetStatistics();
Console.WriteLine($"Position pool reuse rate: {positionStats.ReuseRate:P1}");

// Generate comprehensive report
var report = componentPoolManager.GenerateReport();
File.WriteAllText("component_pool_report.txt", report);
```

### Expected Metrics

- **Reuse Rate**: Target 70-85% (lower during startup, higher in steady state)
- **Utilization Rate**: Should stay below 80% (pool not undersized)
- **Total Created**: Should stabilize after warmup period

### Performance Profiling

```csharp
// Before optimization
var sw = Stopwatch.StartNew();
for (int i = 0; i < 10000; i++)
{
    var pos = new Position(x, y);
    // ... use pos ...
}
sw.Stop();
Console.WriteLine($"Without pooling: {sw.ElapsedMilliseconds}ms");

// After optimization
sw.Restart();
for (int i = 0; i < 10000; i++)
{
    var pos = poolManager.RentPosition();
    pos.X = x;
    pos.Y = y;
    // ... use pos ...
    poolManager.ReturnPosition(pos);
}
sw.Stop();
Console.WriteLine($"With pooling: {sw.ElapsedMilliseconds}ms");
```

## Best Practices

1. **Always use try-finally**: Ensure components are returned even on exceptions
2. **Reset state**: Pools automatically reset to default, but double-check for reference types
3. **Monitor reuse rates**: Low reuse rates indicate pooling may not be beneficial
4. **Adjust pool sizes**: Based on actual usage patterns and memory constraints
5. **Profile in production scenarios**: Pooling benefits vary with entity counts

## Migration Guide

### From Direct Component Access

```csharp
// Before
var position = entity.Get<Position>();
position.X += deltaX;
entity.Set(position);

// After (only for temporary calculations)
var tempPos = poolManager.RentPosition();
try
{
    tempPos.X = position.X + deltaX;
    tempPos.Y = position.Y + deltaY;

    if (IsValid(tempPos))
    {
        entity.Set(tempPos);
    }
}
finally
{
    poolManager.ReturnPosition(tempPos);
}
```

## Troubleshooting

### Low Reuse Rate (<50%)

- Pool size may be too large
- Components not being returned properly
- Check for memory leaks (retained references)

### High Utilization (>90%)

- Pool size too small, increase maxSize
- Too many concurrent operations
- Consider adding more pools

### Memory Not Decreasing

- Component pools reduce allocation rate, not total memory
- GC will collect over time, benefits show in GC metrics
- Use profiler to verify allocation reduction

## Future Enhancements

1. **Auto-tuning pool sizes**: Based on runtime metrics
2. **Warmup strategies**: Pre-allocate pools during loading screens
3. **Per-scene pools**: Different pool configurations per game scene
4. **Integration with entity pools**: Coordinated entity+component lifecycle
