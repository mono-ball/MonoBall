# API Documentation

This folder previously contained API documentation that was out of sync with the actual implementation.

## What Happened?

Two large documentation files (1,776 lines total) were removed because they described planned/proposed APIs that were never implemented:

- ❌ **MAP_STREAMING_INTEGRATION.md** (1,246 lines) - Described a distance-based streaming system with configurable radius
- ❌ **ModAPI.md** (530 lines) - Described a ModBase/ModContext API

## Where to Find Correct Documentation

### For Map Streaming
The actual implementation is simpler than documented:
- **Location:** `MonoBallFramework.Game/Systems/MapStreamingSystem.cs`
- **Component:** `MonoBallFramework.Game/Components/Components/MapStreaming.cs`
- **Behavior:** Connection-based (not distance-based), loads ALL connected maps immediately
- **No configuration:** No streaming radius, fixed Pokemon-style behavior

### For Modding API
The actual modding system uses ScriptBase:
- **Documentation:** `/docs/modding/API-REFERENCE.md` ✅ (CORRECT)
- **Getting Started:** `/docs/modding/getting-started.md` ✅ (CORRECT)
- **Base Class:** `ScriptBase` (not ModBase)
- **Context:** `ScriptContext` (not ModContext)
- **Event Subscription:** `On<TEvent>()`, `OnEntity<T>()`, `OnTile<T>()` methods

## Contributing New API Documentation

If you want to add API documentation here:

1. ✅ **Verify the implementation exists** - Read the actual source code first
2. ✅ **Test your examples** - Make sure code examples actually compile and run
3. ✅ **Use real type names** - Get namespaces and class names correct
4. ✅ **Link to source files** - Help readers find the implementation
5. ✅ **Keep it in sync** - Update docs when code changes

---

**Audit Date:** December 4, 2025  
**See:** `/docs/DOCUMENTATION_AUDIT_RESULTS.md` for full details

