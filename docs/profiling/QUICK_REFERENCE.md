# Profiling Quick Reference Card

**Last Updated:** 2025-11-16
**Purpose:** Fast access to essential profiling commands

---

## ⚡ Quick Start (Copy & Paste)

### 1. Install Tools (One-Time Setup)
```bash
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-counters
```

### 2. Build and Run
```bash
cd /mnt/c/Users/nate0/RiderProjects/PokeSharp
dotnet build -c Release
cd PokeSharp.Game
dotnet run --configuration Release
```

### 3. Find Process ID
```bash
ps aux | grep PokeSharp | grep -v grep
# Note the PID (second column)
```

### 4. Monitor Live (60 seconds)
```bash
dotnet-counters monitor --process-id <PID> \
  --counters System.Runtime[gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,alloc-rate]
```

### 5. Collect Allocation Trace
```bash
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x1:5 \
  --duration 00:00:30 \
  --output pokesharp-allocations.nettrace
```

### 6. Analyze Results
```bash
dotnet-trace report pokesharp-allocations.nettrace --top-allocations
```

---

## 📊 Expected Baseline Metrics

### Current (Before Optimizations)
```
Gen 0 GC Count:        ~46-50 /sec
Gen 1 GC Count:        ~2-4 /sec
Gen 2 GC Count:        ~14-16 /sec
Allocation Rate:       700,000-800,000 B/sec (700-800 KB/sec)
```

### Target (After All Optimizations)
```
Gen 0 GC Count:        5-8 /sec         ✅ -89%
Gen 1 GC Count:        1-2 /sec         ✅ -50%
Gen 2 GC Count:        0-1 /sec         ✅ -93%
Allocation Rate:       80,000-130,000 B/sec (80-130 KB/sec) ✅ -84%
```

---

## 🔍 Code Search Commands

### Find LINQ in Systems
```bash
cd /mnt/c/Users/nate0/RiderProjects/PokeSharp
grep -rn "\.Where\|\.Select\|\.ToList\|\.ToArray\|\.FirstOrDefault" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/ \
  PokeSharp.Engine.Systems/
```

### Find Collection Allocations
```bash
grep -rn "new List\|new Dictionary\|new HashSet" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/ \
  PokeSharp.Engine.Systems/
```

### Find Logging in Hot Paths
```bash
grep -rn "Log(Information\|Debug\|Trace)" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/ | \
  grep -v "PerformanceMonitor"
```

### Find String Operations
```bash
grep -rn '\$"\|String\.Concat\|String\.Format' \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/
```

---

## 🎯 Known Issues to Fix

### 🔴 HIGH PRIORITY

#### SystemManager LINQ (60-80 KB/sec)
**File:** `PokeSharp.Engine.Systems/Management/SystemManager.cs`
**Lines:** 232, 275
**Issue:** `.Where(s => s.Enabled).ToArray()` runs 120x/sec
**Status:** ⏳ Not fixed

#### PathfindingSystem LINQ (40-80 KB/sec - suspected)
**File:** `PokeSharp.Game.Systems/NPCs/PathfindingSystem.cs`
**Status:** 🔍 Needs manual review

---

## 📁 Documentation Files

| File | Purpose |
|------|---------|
| README.md | Overview and navigation |
| PHASE_3_PROFILING_STRATEGY.md | Complete profiling methodology |
| INVESTIGATION_TARGETS.md | Specific code patterns to check |
| CODEBASE_ANALYSIS_SUMMARY.md | Code review findings |
| QUICK_REFERENCE.md | This file - fast command access |

---

## 🔧 Profiling Workflow

### Before Each Session
- [ ] Build in Release mode
- [ ] Close other apps
- [ ] Let game run for 30+ seconds to stabilize

### During Session
1. **Monitor** - 60 seconds with dotnet-counters
2. **Collect** - 30 seconds with dotnet-trace (3x for consistency)
3. **Analyze** - Look for >50 KB/sec allocations

### After Session
- [ ] Document findings
- [ ] Prioritize by KB/sec impact
- [ ] Implement highest impact fix first
- [ ] Re-profile to verify improvement

---

## 💡 Pro Tips

### Interpreting dotnet-counters Output
```
Gen 0 GC Count:  48/sec  ← Too high! (normal: 5-10)
Allocation Rate: 750 KB  ← Too high! (normal: 80-150)
```

### Interpreting dotnet-trace Output
Look for these patterns:
- **String allocations** - String.Concat, interpolation
- **LINQ allocations** - Enumerable.Where, ToArray, ToList
- **Collection growth** - List.Grow, Dictionary.Resize
- **Closure allocations** - DisplayClass

### Quick Validation After Fix
```bash
# Before fix
dotnet-counters monitor -p <PID> --counters System.Runtime[gen-0-gc-count]
# Note Gen0 count after 60 seconds

# Apply fix, restart game

# After fix
dotnet-counters monitor -p <PID> --counters System.Runtime[gen-0-gc-count]
# Compare Gen0 count - should be lower
```

---

## 🚨 Emergency Commands

### Game Frozen During Profiling?
```bash
# Find and kill process
pkill -9 PokeSharp

# Or specific PID
kill -9 <PID>
```

### Trace File Too Large?
```bash
# Reduce duration
dotnet-trace collect -p <PID> --duration 00:00:10

# Or compress
gzip pokesharp-allocations.nettrace
```

### Can't Find Process?
```bash
# List all dotnet processes
ps aux | grep dotnet

# Or use pgrep
pgrep -fl PokeSharp
```

---

## 📞 Need Help?

### See Full Documentation
- [README.md](./README.md) - Start here
- [PHASE_3_PROFILING_STRATEGY.md](./PHASE_3_PROFILING_STRATEGY.md) - Detailed methodology
- [INVESTIGATION_TARGETS.md](./INVESTIGATION_TARGETS.md) - What to profile

### External Resources
- [dotnet-trace docs](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [dotnet-counters docs](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [.NET Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/performance/)

---

## 🎓 Common Allocation Patterns

### LINQ (Bad)
```csharp
❌ var results = collection.Where(x => x.Active).ToList();
✅ var results = new List<T>(capacity);
   foreach (var item in collection)
       if (item.Active) results.Add(item);
```

### String Concatenation (Bad)
```csharp
❌ var key = $"{category}/{name}";
✅ var key = string.Concat(category, "/", name);
```

### Collection Growth (Bad)
```csharp
❌ var list = new List<T>(); // Capacity 4 → grows
✅ var list = new List<T>(expectedSize);
```

### Collection Reuse (Good)
```csharp
✅ private readonly List<T> _buffer = new(128);

   public void Update() {
       _buffer.Clear(); // Reuses capacity
       foreach (var item in query)
           _buffer.Add(item);
   }
```

---

**Print this page for desk reference!** 🖨️

---

**Last Updated:** 2025-11-16
**Status:** Ready for immediate use
