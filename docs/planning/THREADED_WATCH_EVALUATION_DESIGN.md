# Threaded Watch Evaluation - Design Document

**Status:** Design Phase
**Priority:** HIGH (Performance Critical)
**Complexity:** High (6-10 hours)
**Date:** November 25, 2025

---

## 🎯 Problem Statement

### Current Situation
Watch expressions are currently evaluated synchronously on the main game thread during each update cycle. This causes:

- **Frame drops** when evaluating expensive expressions
- **UI lag** when many watches are active
- **Game stuttering** with complex object inspection
- **Poor user experience** when watches evaluate slowly

### Example Performance Issues
```csharp
// Expensive watch expressions that block the main thread:
watch add all_entities World.GetAllEntities()           // 10,000+ entities
watch add distance_calc Player.CalculateDistanceTo(target)  // Complex math
watch add json_data JsonSerializer.Serialize(gameState)     // Serialization
watch add query_result ECS.Query<Transform, Sprite>()       // Large queries
```

**Current:** All of these run on the main thread every 500ms, causing noticeable frame drops.

---

## 🎯 Goals

### Primary Goals
1. **Eliminate frame drops** caused by watch evaluation
2. **Maintain responsiveness** of the watch panel UI
3. **Keep the system simple** and maintainable
4. **Ensure thread safety** for game state access

### Secondary Goals
1. Support cancellation of long-running evaluations
2. Provide timeout protection (5-10 seconds max)
3. Show evaluation time in the UI
4. Allow marking expressions as "main thread only" for non-thread-safe APIs

---

## 🏗️ Architecture Options

### Option 1: Background Thread with Queue ⭐ RECOMMENDED
**Pros:**
- Simple design
- Predictable behavior
- Easy to debug
- Clear separation of concerns

**Cons:**
- Need to handle thread synchronization
- Results arrive asynchronously

```csharp
class WatchEvaluationService
{
    private readonly Thread _workerThread;
    private readonly BlockingCollection<WatchJob> _jobQueue;
    private readonly ConcurrentQueue<WatchResult> _resultQueue;
    private bool _running;

    public void QueueEvaluation(WatchJob job);
    public bool TryGetResult(out WatchResult result);

    // Worker thread continuously processes jobs
    private void ProcessJobs();
}
```

### Option 2: Task-Based Async (TPL)
**Pros:**
- Uses .NET Task infrastructure
- Automatic thread pool management
- Support for async/await

**Cons:**
- More complex error handling
- Harder to control execution
- Task overhead

```csharp
class AsyncWatchEvaluator
{
    public async Task<object?> EvaluateAsync(
        string expression,
        Func<object?> evaluator,
        CancellationToken ct);
}
```

### Option 3: Hybrid Approach
**Pros:**
- Best of both worlds
- Can choose per-watch basis

**Cons:**
- Most complex
- Need to maintain two code paths

**Recommendation:** Start with **Option 1** (Background Thread with Queue) for simplicity and predictability.

---

## 📐 Detailed Design (Option 1)

### Core Components

#### 1. WatchJob
```csharp
public class WatchJob
{
    public string WatchName { get; init; }
    public string Expression { get; init; }
    public Func<object?> Evaluator { get; init; }
    public Func<bool>? ConditionEvaluator { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public DateTime QueuedAt { get; init; }
}
```

#### 2. WatchResult
```csharp
public class WatchResult
{
    public string WatchName { get; init; }
    public object? Value { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan EvaluationTime { get; init; }
    public DateTime CompletedAt { get; init; }
}
```

#### 3. WatchEvaluationService
```csharp
public class WatchEvaluationService : IDisposable
{
    private readonly Thread _workerThread;
    private readonly BlockingCollection<WatchJob> _jobQueue;
    private readonly ConcurrentQueue<WatchResult> _resultQueue;
    private readonly CancellationTokenSource _shutdownToken;
    private bool _running;

    public WatchEvaluationService()
    {
        _jobQueue = new BlockingCollection<WatchJob>(boundedCapacity: 100);
        _resultQueue = new ConcurrentQueue<WatchResult>();
        _shutdownToken = new CancellationTokenSource();

        _workerThread = new Thread(ProcessJobs)
        {
            Name = "WatchEvaluator",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        _running = true;
        _workerThread.Start();
    }

    public void QueueEvaluation(WatchJob job)
    {
        if (!_jobQueue.TryAdd(job, millisecondsTimeout: 100))
        {
            // Queue full - skip this evaluation
            // Could also implement priority queue to drop old jobs
        }
    }

    public bool TryGetResult(out WatchResult? result)
    {
        return _resultQueue.TryDequeue(out result);
    }

    private void ProcessJobs()
    {
        while (_running && !_shutdownToken.Token.IsCancellationRequested)
        {
            try
            {
                if (_jobQueue.TryTake(out var job, millisecondsTimeout: 100))
                {
                    ProcessJob(job);
                }
            }
            catch (Exception ex)
            {
                // Log error, continue processing
            }
        }
    }

    private void ProcessJob(WatchJob job)
    {
        var sw = Stopwatch.StartNew();
        var result = new WatchResult { WatchName = job.WatchName };

        try
        {
            // Check condition first (if present)
            if (job.ConditionEvaluator != null)
            {
                bool conditionMet = job.ConditionEvaluator();
                if (!conditionMet)
                {
                    result.Value = null; // Condition not met, skip evaluation
                    result.CompletedAt = DateTime.Now;
                    result.EvaluationTime = sw.Elapsed;
                    _resultQueue.Enqueue(result);
                    return;
                }
            }

            // Evaluate with timeout
            var task = Task.Run(() => job.Evaluator(), job.CancellationToken);
            if (task.Wait(TimeSpan.FromSeconds(10), job.CancellationToken))
            {
                result.Value = task.Result;
                result.HasError = false;
            }
            else
            {
                result.HasError = true;
                result.ErrorMessage = "Evaluation timeout (10 seconds)";
            }
        }
        catch (OperationCanceledException)
        {
            result.HasError = true;
            result.ErrorMessage = "Evaluation cancelled";
        }
        catch (Exception ex)
        {
            result.HasError = true;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.CompletedAt = DateTime.Now;
            result.EvaluationTime = sw.Elapsed;
            _resultQueue.Enqueue(result);
        }
    }

    public void Dispose()
    {
        _running = false;
        _shutdownToken.Cancel();
        _workerThread.Join(TimeSpan.FromSeconds(2));
        _jobQueue.Dispose();
        _shutdownToken.Dispose();
    }
}
```

---

## 🔒 Thread Safety Considerations

### Challenge: Game State Access
MonoGame and game objects are **not thread-safe**. Accessing them from a background thread can cause:
- Race conditions
- Null reference exceptions
- State corruption
- Crashes

### Solutions

#### Solution 1: Data Snapshot (RECOMMENDED)
Capture data on main thread, evaluate on background thread.

```csharp
// Main thread: Capture data
var playerPosition = Player.Position; // Copy value
var entities = World.GetAllEntities().ToList(); // Snapshot collection

// Background thread: Evaluate using snapshot
watch add player_x playerPosition.X  // Safe - using copied data
watch add entity_count entities.Count // Safe - using snapshot
```

#### Solution 2: Main Thread Marker
Some expressions MUST run on main thread.

```csharp
public class WatchEntry
{
    public bool RequiresMainThread { get; set; }
}

// In WatchPanel.AddWatch:
public bool AddWatch(string name, string expression, bool requiresMainThread = false)
{
    // If requiresMainThread is true, evaluate on main thread
    // Otherwise, queue for background evaluation
}
```

#### Solution 3: Synchronization Context
Use `SynchronizationContext` to marshal back to main thread when needed.

```csharp
var mainThreadContext = SynchronizationContext.Current;

// In background thread:
mainThreadContext.Post(_ =>
{
    var result = Player.GetPosition(); // Access game state safely
}, null);
```

---

## 🔄 Integration with Existing Watch System

### Current Flow
```
1. Update() called every frame
2. Check if update interval elapsed
3. Evaluate all watches synchronously
4. Update display
```

### New Flow
```
1. Update() called every frame
2. Check if update interval elapsed
3. Queue all watch evaluations (non-blocking)
4. Check for completed results
5. Update display with results
6. Continue rendering (no blocking!)
```

### WatchPanel Changes

```csharp
public class WatchPanel : Panel
{
    private WatchEvaluationService? _evaluationService;
    private readonly Dictionary<string, CancellationTokenSource> _watchCancellations;

    public override void Initialize()
    {
        base.Initialize();
        _evaluationService = new WatchEvaluationService();
    }

    private void UpdateWatchValues()
    {
        foreach (var key in _watchKeys)
        {
            var entry = _watches[key];

            // Create cancellation token for this watch
            var cts = new CancellationTokenSource();
            _watchCancellations[key] = cts;

            // Queue evaluation (non-blocking)
            _evaluationService.QueueEvaluation(new WatchJob
            {
                WatchName = entry.Name,
                Expression = entry.Expression,
                Evaluator = entry.ValueGetter,
                ConditionEvaluator = entry.ConditionEvaluator,
                CancellationToken = cts.Token,
                QueuedAt = DateTime.Now
            });
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        // Check for completed evaluations
        while (_evaluationService.TryGetResult(out var result))
        {
            if (_watches.TryGetValue(result.WatchName, out var entry))
            {
                // Update entry with result
                entry.PreviousValue = entry.LastValue;
                entry.LastValue = result.Value;
                entry.HasError = result.HasError;
                entry.ErrorMessage = result.ErrorMessage;
                entry.LastUpdated = result.CompletedAt;
                entry.UpdateCount++;

                // Track history if value changed
                if (!Equals(entry.LastValue, entry.PreviousValue))
                {
                    entry.History.Add((result.CompletedAt, result.Value));
                    if (entry.History.Count > entry.MaxHistorySize)
                    {
                        entry.History.RemoveAt(0);
                    }

                    // Check alerts
                    CheckAlert(entry);
                }

                // Update comparison
                UpdateComparison(entry);
            }
        }

        // Update display if results arrived
        if (_resultQueue.Count > 0)
        {
            UpdateWatchDisplay();
        }
    }

    public override void Dispose()
    {
        // Cancel all pending evaluations
        foreach (var cts in _watchCancellations.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _watchCancellations.Clear();

        _evaluationService?.Dispose();
        base.Dispose();
    }
}
```

---

## 📊 Performance Impact

### Expected Improvements
- **Frame time:** Stable 16.67ms (60 FPS) even with expensive watches
- **Watch updates:** Still happen at configured interval (500ms default)
- **Latency:** Slight delay for results (acceptable tradeoff)
- **CPU usage:** Moved to background thread (better distribution)

### Measurements
Add performance tracking to watch entries:

```csharp
public class WatchEntry
{
    // Existing properties...

    // Performance tracking
    public TimeSpan LastEvaluationTime { get; set; }
    public TimeSpan AverageEvaluationTime { get; set; }
    public int SlowEvaluationCount { get; set; } // Count of evals > 10ms
}
```

Display in UI:
```
  [1] expensive_watch [📌]
      Expression: World.GetAllEntities()
      Value:      10,234 entities
      Eval Time:  23.5ms (avg: 18.2ms) ⚠️  SLOW
      Updated:    just now (45 times)
```

---

## 🧪 Testing Strategy

### Unit Tests
1. Test job queuing
2. Test result retrieval
3. Test cancellation
4. Test timeout
5. Test error handling

### Integration Tests
1. Test with simple expressions
2. Test with expensive expressions
3. Test with main thread access
4. Test concurrent evaluations
5. Test watch add/remove during evaluation

### Performance Tests
1. Measure frame time with 50 watches
2. Measure evaluation latency
3. Measure memory usage
4. Stress test with very expensive watches

---

## 🚀 Implementation Plan

### Phase 1: Core Service (2-3 hours)
- [ ] Create `WatchEvaluationService` class
- [ ] Implement job queue
- [ ] Implement result queue
- [ ] Implement worker thread
- [ ] Add timeout protection
- [ ] Add cancellation support

### Phase 2: Integration (2-3 hours)
- [ ] Update `WatchPanel` to use service
- [ ] Modify `UpdateWatchValues()` to queue jobs
- [ ] Modify `Update()` to process results
- [ ] Add proper disposal/cleanup
- [ ] Test with existing watches

### Phase 3: Performance Tracking (1-2 hours)
- [ ] Add evaluation time tracking
- [ ] Update UI to show eval time
- [ ] Add "slow watch" indicators
- [ ] Add performance stats command

### Phase 4: Polish & Safety (1-2 hours)
- [ ] Add comprehensive error handling
- [ ] Add logging for debugging
- [ ] Add "main thread only" option (if needed)
- [ ] Documentation and examples

---

## 🎯 Success Criteria

✅ **Frame rate remains stable** (60 FPS) even with 50 active watches
✅ **No crashes** from thread safety issues
✅ **Results appear** within 1 second of evaluation
✅ **Expensive watches** don't block the game
✅ **Errors are handled gracefully** without crashing
✅ **Performance metrics** are visible in the UI

---

## 📝 Future Enhancements

After basic threading is working:

1. **Priority Queue:** Prioritize pinned watches or recently viewed watches
2. **Adaptive Intervals:** Automatically slow down expensive watches
3. **Parallel Evaluation:** Multiple worker threads for better throughput
4. **Smart Scheduling:** Distribute evaluations across multiple frames
5. **Expression Profiling:** Automatically identify and warn about expensive expressions

---

**This design provides a solid foundation for performant, thread-safe watch evaluation while maintaining simplicity and debuggability.**



