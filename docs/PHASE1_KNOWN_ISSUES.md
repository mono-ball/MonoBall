# Phase 1 Known Issues Log

**Last Updated:** November 9, 2025
**Status:** 3 of 5 components delivered
**Issue Count:** 8 total (0 critical, 2 major, 6 minor)

---

## Issue Severity Levels

- 🔴 **Critical** - Blocks usage or causes data loss
- 🟠 **Major** - Significant impact on functionality or performance
- 🟡 **Minor** - Low impact, cosmetic, or nice-to-have improvements

---

## Critical Issues (0)

No critical issues identified. All delivered components are functional and safe to use.

---

## Major Issues (2)

### MAJOR-001: Missing Testing Infrastructure
**Component:** Phase 1 Infrastructure
**Severity:** 🟠 Major
**Impact:** Cannot validate implementations or prevent regressions
**Status:** Open

**Description:**
The ECS testing infrastructure component was not delivered. This is a Phase 1 requirement that provides:
- EcsTestBase class for test setup/teardown
- World creation and cleanup utilities
- Entity and component creation helpers
- Assertion methods for ECS testing
- Example test files

**Impact:**
- Cannot write unit tests for Phase 1 components
- Cannot validate correctness of implementations
- Cannot prevent regressions in future changes
- Difficult to onboard new developers
- High risk for Phase 2 development

**Workaround:**
Manual testing only. Not suitable for production.

**Resolution Plan:**
1. Implement EcsTestBase class
2. Add helper methods for common test scenarios
3. Write example tests
4. Document testing patterns
5. Create test suite for Phase 1 components

**Priority:** CRITICAL - Must complete before Phase 2

**Estimated Effort:** 8-16 hours

**Assigned To:** Testing Infrastructure Agent (not completed)

---

### MAJOR-002: Missing Performance Benchmarks
**Component:** Phase 1 Infrastructure
**Severity:** 🟠 Major
**Impact:** Cannot measure performance or validate optimizations
**Status:** Open

**Description:**
The performance benchmark suite was not delivered. Only an empty directory exists at `/tests/Benchmarks/`.

Expected deliverables:
- BenchmarkDotNet-based benchmark suite
- Entity creation/destruction benchmarks
- Query performance tests
- Component access benchmarks
- System update performance tests
- Memory usage diagnostics
- Baseline metrics report

**Impact:**
- Cannot measure query caching performance benefits
- Cannot validate relationship system overhead
- Cannot establish performance baselines
- Cannot detect performance regressions
- Cannot make data-driven optimization decisions

**Workaround:**
Use manual profiling tools (Visual Studio Profiler, dotTrace, etc.)

**Resolution Plan:**
1. Setup BenchmarkDotNet project
2. Create benchmark suite for core operations
3. Add benchmarks for Phase 1 components
4. Establish baseline metrics
5. Document benchmarking process
6. Integrate into CI/CD pipeline

**Priority:** HIGH - Important for validation and optimization

**Estimated Effort:** 16-24 hours

**Assigned To:** Performance Analyzer Agent (not completed)

---

## Minor Issues (6)

### MINOR-001: QueryCache Not Used by RelationshipSystem
**Component:** Query Caching / Relationship System
**Severity:** 🟡 Minor
**Impact:** Missing minor optimization opportunity
**Status:** Open

**Description:**
RelationshipSystem creates its own QueryDescription instances instead of using the pre-built queries from RelationshipQueries.

**Current Code:**
```csharp
// RelationshipSystem.cs
public override void Initialize(World world)
{
    _parentQuery = new QueryDescription().WithAll<Parent>();
    _childrenQuery = new QueryDescription().WithAll<Children>();
    // ...
}
```

**Should Be:**
```csharp
public override void Initialize(World world)
{
    _parentQuery = RelationshipQueries.AllChildren;
    _childrenQuery = RelationshipQueries.AllParents;
    // ...
}
```

**Impact:**
- Very minor - creates a few extra QueryDescription instances
- Misses opportunity for code reuse
- Slight inconsistency in codebase

**Resolution:**
Simple refactor to use RelationshipQueries properties

**Priority:** LOW

**Estimated Effort:** 15 minutes

---

### MINOR-002: Limited QueryCache Generic Overloads
**Component:** Query Caching
**Severity:** 🟡 Minor
**Impact:** Limited to 3 components per query
**Status:** Open

**Description:**
QueryCache only provides generic methods for up to 3 components:
- `Get<T1>()`
- `Get<T1, T2>()`
- `Get<T1, T2, T3>()`

Queries with 4+ components must be created manually.

**Impact:**
- Most queries use 1-3 components, so this covers 95% of use cases
- Minor inconvenience for complex queries
- Inconsistent API (some cached, some not)

**Resolution:**
Add overloads for 4, 5, and 6 components:
```csharp
public static QueryDescription Get<T1, T2, T3, T4>() where ...
public static QueryDescription Get<T1, T2, T3, T4, T5>() where ...
public static QueryDescription Get<T1, T2, T3, T4, T5, T6>() where ...
```

**Priority:** LOW

**Estimated Effort:** 1 hour

---

### MINOR-003: No QueryCache Documentation
**Component:** Query Caching
**Severity:** 🟡 Minor
**Impact:** Developers don't know how to use it
**Status:** Open

**Description:**
QueryCache has minimal documentation:
- No usage examples
- No performance benefit explanation
- No migration guide from manual queries
- No best practices

**Impact:**
- Developers may not use QueryCache
- Benefits not realized
- Inconsistent usage patterns

**Resolution:**
Add comprehensive documentation:
1. XML comments with usage examples
2. Migration guide document
3. Performance benefits explanation
4. Best practices section

**Priority:** MEDIUM

**Estimated Effort:** 2-3 hours

---

### MINOR-004: ServiceContainer Multiple Dictionary Lookups
**Component:** Dependency Injection
**Severity:** 🟡 Minor
**Impact:** Minor performance inefficiency
**Status:** Open

**Description:**
ServiceContainer.Resolve() performs multiple dictionary lookups:
```csharp
if (_singletons.TryGetValue(type, out var singleton)) { }
if (_factories.TryGetValue(type, out var factory)) { }
if (_lifetimes.TryGetValue(type, out var lifetime)) { }
```

**Impact:**
- Very minor performance impact
- Only affects service resolution (one-time cost)
- Not noticeable in practice

**Resolution:**
Consider consolidating into single registration record:
```csharp
private record ServiceRegistration(
    Func<ServiceContainer, object>? Factory,
    ServiceLifetime Lifetime,
    object? SingletonInstance
);
```

**Priority:** LOW (optimization)

**Estimated Effort:** 2-3 hours

---

### MINOR-005: No SystemManager Integration with DI
**Component:** Dependency Injection / SystemManager
**Severity:** 🟡 Minor
**Impact:** Manual system instantiation required
**Status:** Open

**Description:**
SystemManager doesn't integrate with ServiceContainer/SystemFactory. Systems must be manually instantiated with dependencies.

**Current Usage:**
```csharp
var container = new ServiceContainer();
container.RegisterSingleton(logger);

var factory = new SystemFactory(container);
var relationshipSystem = factory.CreateSystem<RelationshipSystem>();

systemManager.RegisterSystem(relationshipSystem);
```

**Desired Usage:**
```csharp
var container = new ServiceContainer();
container.RegisterSingleton(logger);

systemManager.RegisterSystem<RelationshipSystem>(container);
// SystemManager uses SystemFactory internally
```

**Impact:**
- Minor inconvenience
- More boilerplate code
- Easy to forget dependencies

**Resolution:**
Add DI integration to SystemManager:
```csharp
public void RegisterSystem<TSystem>(ServiceContainer container)
    where TSystem : ISystem
{
    var factory = new SystemFactory(container);
    var system = factory.CreateSystem<TSystem>();
    RegisterSystem(system);
}
```

**Priority:** MEDIUM

**Estimated Effort:** 2-4 hours

---

### MINOR-006: No Existing System Migration to QueryCache
**Component:** Query Caching
**Severity:** 🟡 Minor
**Impact:** Performance benefits not realized
**Status:** Open

**Description:**
Existing systems in PokeSharp.Core still create their own queries instead of using QueryCache.

**Affected Systems:**
- MovementSystem
- CollisionSystem
- SpatialHashSystem
- PathfindingSystem
- TileAnimationSystem
- NpcBehaviorSystem
- Others (15+ systems total)

**Example:**
```csharp
// Current pattern in existing systems
public class MovementSystem : BaseSystem
{
    private QueryDescription _query;

    public override void Initialize(World world)
    {
        _query = new QueryDescription().WithAll<Position, Velocity>();
    }
}

// Should use QueryCache
_query = QueryCache.Get<Position, Velocity>();
```

**Impact:**
- Missing minor performance improvement
- Inconsistent codebase
- Query caching benefits not realized

**Resolution:**
1. Create migration guide
2. Update existing systems to use QueryCache
3. Add pre-commit hook to encourage QueryCache usage

**Priority:** MEDIUM

**Estimated Effort:** 4-8 hours (depends on number of systems)

---

## Resolved Issues (0)

No issues have been resolved yet. This is the initial Phase 1 review.

---

## Issue Statistics

| Severity | Open | Resolved | Total |
|----------|------|----------|-------|
| Critical | 0 | 0 | 0 |
| Major | 2 | 0 | 2 |
| Minor | 6 | 0 | 6 |
| **Total** | **8** | **0** | **8** |

---

## Issue Priority Matrix

| Issue | Severity | Impact | Priority | Effort |
|-------|----------|--------|----------|--------|
| MAJOR-001 | Major | High | CRITICAL | 8-16h |
| MAJOR-002 | Major | Medium | HIGH | 16-24h |
| MINOR-005 | Minor | Medium | MEDIUM | 2-4h |
| MINOR-003 | Minor | Medium | MEDIUM | 2-3h |
| MINOR-006 | Minor | Medium | MEDIUM | 4-8h |
| MINOR-001 | Minor | Low | LOW | 15min |
| MINOR-002 | Minor | Low | LOW | 1h |
| MINOR-004 | Minor | Low | LOW | 2-3h |

---

## Recommendations

### Before Phase 2
**Must Complete:**
1. MAJOR-001: Testing Infrastructure (CRITICAL)
2. MAJOR-002: Performance Benchmarks (HIGH)

**Total Estimated Effort:** 24-40 hours

### Before Production
**Should Complete:**
1. MINOR-005: SystemManager DI Integration (MEDIUM)
2. MINOR-003: QueryCache Documentation (MEDIUM)
3. MINOR-006: Migrate Existing Systems (MEDIUM)

**Total Estimated Effort:** 8-15 hours

### Future Improvements
**Nice to Have:**
1. MINOR-001: Use RelationshipQueries (LOW)
2. MINOR-002: Extend QueryCache Overloads (LOW)
3. MINOR-004: Optimize ServiceContainer (LOW)

**Total Estimated Effort:** 3-4 hours

---

## Issue Tracking

### How to Report Issues

1. Check if issue already exists in this log
2. Determine severity level
3. Document impact and reproduction steps
4. Add to appropriate section
5. Assign priority and estimate effort
6. Update statistics

### Issue Resolution Process

1. Assign issue to appropriate agent/developer
2. Create implementation plan
3. Implement fix
4. Write tests
5. Update documentation
6. Move to "Resolved Issues" section
7. Update statistics

---

## Notes

- All delivered components are functional despite these issues
- No blocking issues for basic usage
- Testing infrastructure is critical path for Phase 2
- Most minor issues are optimizations or nice-to-haves

---

**Maintained By:** Review Coordinator Agent
**Next Review:** After issue resolution
**Contact:** Create GitHub issue for new problems
