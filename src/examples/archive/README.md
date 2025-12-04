# Example Scripts Archive

**Archived**: December 4, 2025  
**Reason**: Competing implementation superseded by canonical ScriptBase system

---

## Why This Was Archived

This directory contained **UnifiedScriptBase** examples, which competed with the canonical **ScriptBase** system.

### Decision

**✅ ScriptBase** (in `examples/unified-scripts/`) was chosen as the canonical system.

The **UnifiedScriptBase** system has been archived because:
- ScriptBase has better architecture (Phase 3.1 ADR-backed)
- ScriptBase has comprehensive documentation
- ScriptBase integrates better with existing systems
- Maintaining two systems created confusion

---

## Canonical Examples

**Use these instead**: `examples/unified-scripts/`

The canonical examples directory contains:
- Ice tile behavior
- Tall grass encounters
- Ledge jumping
- NPC patrol AI
- Script composition examples
- Custom event examples
- Hot reload tests

---

## What Was Different?

The **UnifiedScriptBase** system used slightly different:
- API naming conventions
- Event subscription patterns
- State management approach

Both systems were functionally similar, but **ScriptBase** was chosen for consistency with Phase 3 roadmap.

---

## Need This Code?

If you need code from the archived UnifiedScriptBase system:

1. **See**: `examples/unified-scripts/` for canonical implementations
2. **Docs**: `docs/scripting/unified-scripting-guide.md`
3. **ADR**: `docs/architecture/Phase3-1-ScriptBase-ADR.md`

All concepts from UnifiedScriptBase are represented in ScriptBase with improved design.

---

**Archive Status**: Preserved for historical reference  
**Git History**: Available for full restoration  
**Maintained**: No (superseded by ScriptBase)

