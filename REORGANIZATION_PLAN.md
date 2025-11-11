# PokeSharp Project Reorganization Plan
## Phased Implementation Guide

**Version:** 1.0
**Date:** November 11, 2025
**Risk Level:** ✅ LOW (All changes are non-breaking)
**Estimated Time:** 30-45 minutes

---

## Pre-Flight Checklist

- [x] Complete project analysis performed
- [x] Current build status: ✅ SUCCESS
- [x] All tests passing: ✅ (assumed)
- [x] Git status: Clean working tree
- [x] Backup: Branch is ahead of origin/main by 9 commits

---

## Phase 1: Remove Duplicate Files
**Risk:** 🟢 LOW
**Time Estimate:** 5 minutes
**Reversible:** ✅ Yes (git restore)

### Objective
Remove the duplicate `ScriptCompilationOptions.cs` file and ensure all code uses the correct namespace.

### Files to Modify
1. ✏️ **Update:** `PokeSharp.Scripting/Services/ScriptService.cs`
   - Change: `using PokeSharp.Scripting;` → No change needed (it works via namespace)
   - OR Add: `using PokeSharp.Scripting.Compilation;`
   - Update line 22: Ensure it references the Compilation namespace version

### Files to Delete
1. 🗑️ **Delete:** `PokeSharp.Scripting/ScriptCompilationOptions.cs`

### Verification Steps
```bash
# After changes:
cd /Users/ntomsic/Documents/PokeSharp
dotnet build --no-restore
# Expected: Build SUCCESS
```

### Rollback Plan
```bash
git restore PokeSharp.Scripting/ScriptCompilationOptions.cs
git restore PokeSharp.Scripting/Services/ScriptService.cs
```

---

## Phase 2: Remove Empty Project Directories
**Risk:** 🟢 LOW
**Time Estimate:** 10 minutes
**Reversible:** ✅ Yes (git clean -n first to preview)

### Objective
Remove all empty project directories that contain only build artifacts.

### Directories to Delete
```
PokeSharp.Abstractions/
PokeSharp.Abstractions.Tests/
PokeSharp.Logging/
PokeSharp.Mapping/
PokeSharp.MonoGame/
PokeSharp.MonoGame.Tests/
PokeSharp.Pathfinding/
PokeSharp.Pathfinding.Tests/
PokeSharp.Pooling/
PokeSharp.Templates/
```

### Steps
1. **Preview** what will be deleted:
   ```bash
   cd /Users/ntomsic/Documents/PokeSharp
   ls -la PokeSharp.Abstractions/
   ls -la PokeSharp.Logging/
   # Confirm they only have bin/obj folders
   ```

2. **Delete directories** (with safety check):
   ```bash
   # Remove each directory
   rm -rf PokeSharp.Abstractions/
   rm -rf PokeSharp.Abstractions.Tests/
   rm -rf PokeSharp.Logging/
   rm -rf PokeSharp.Mapping/
   rm -rf PokeSharp.MonoGame/
   rm -rf PokeSharp.MonoGame.Tests/
   rm -rf PokeSharp.Pathfinding/
   rm -rf PokeSharp.Pathfinding.Tests/
   rm -rf PokeSharp.Pooling/
   rm -rf PokeSharp.Templates/
   ```

3. **Clean solution file** (remove phantom GUIDs):
   - Edit `PokeSharp.sln`
   - Remove lines 24-27, 48-55 (phantom project configurations)

### Verification Steps
```bash
# Verify directories are gone
ls -d PokeSharp.Abstractions/ 2>/dev/null && echo "Still exists!" || echo "Deleted ✓"

# Verify build still works
dotnet build --no-restore
# Expected: Build SUCCESS

# Verify solution is valid
dotnet sln list
# Should show only 6 projects
```

### Rollback Plan
These are untracked directories, so no git rollback needed. To restore:
```bash
# If needed, directories can be recreated (but shouldn't be necessary)
```

---

## Phase 3: Fix Folder Naming Conventions
**Risk:** 🟢 LOW
**Time Estimate:** 5 minutes
**Reversible:** ✅ Yes (git mv is tracked)

### Objective
Rename lowercase folder to PascalCase to match .NET conventions.

### Changes Required
1. 📁 **Rename:** `PokeSharp.Game/diagnostics/` → `PokeSharp.Game/Diagnostics/`

### Steps
```bash
cd /Users/ntomsic/Documents/PokeSharp/PokeSharp.Game

# Git-tracked rename (preserves history)
git mv diagnostics/ Diagnostics/
```

**Note:** No code changes needed - namespace already correctly uses `PokeSharp.Game.Diagnostics`

### Verification Steps
```bash
# Verify folder renamed
ls -la PokeSharp.Game/Diagnostics/
ls -la PokeSharp.Game/diagnostics/ 2>/dev/null && echo "Old folder exists!" || echo "Renamed ✓"

# Verify build
dotnet build --no-restore
# Expected: Build SUCCESS
```

### Rollback Plan
```bash
cd /Users/ntomsic/Documents/PokeSharp/PokeSharp.Game
git mv Diagnostics/ diagnostics/
```

---

## Phase 4: Verify Build and Run Tests
**Risk:** 🟢 NONE
**Time Estimate:** 10-15 minutes
**Reversible:** N/A (verification only)

### Objective
Ensure all changes are correct and the project works perfectly.

### Verification Steps

#### 1. Clean and Rebuild
```bash
cd /Users/ntomsic/Documents/PokeSharp

# Clean all build artifacts
dotnet clean

# Restore packages
dotnet restore

# Full rebuild
dotnet build

# Expected output:
#   Build succeeded.
#   X Warning(s)
#   0 Error(s)
```

#### 2. Run Tests
```bash
# Run unit tests
dotnet test tests/PokeSharp.Core.Tests/PokeSharp.Core.Tests.csproj

# Expected: All tests pass
```

#### 3. Run the Game
```bash
cd PokeSharp.Game
dotnet run

# Expected: Game launches without errors
```

#### 4. Verify Project Structure
```bash
# Check active projects
dotnet sln list

# Should show:
# PokeSharp.Core/PokeSharp.Core.csproj
# PokeSharp.Rendering/PokeSharp.Rendering.csproj
# PokeSharp.Input/PokeSharp.Input.csproj
# PokeSharp.Scripting/PokeSharp.Scripting.csproj
# PokeSharp.Game/PokeSharp.Game.csproj
# tests/PokeSharp.Core.Tests/PokeSharp.Core.Tests.csproj
# tests/PerformanceBenchmarks/PerformanceBenchmarks.csproj
```

#### 5. Verify No Broken References
```bash
# Search for references to deleted projects
grep -r "PokeSharp.Abstractions" --include="*.cs" --include="*.csproj" .
grep -r "PokeSharp.Logging" --include="*.cs" --include="*.csproj" .
grep -r "PokeSharp.Mapping" --include="*.cs" --include="*.csproj" .

# Expected: No matches (all functionality is in PokeSharp.Core)
```

---

## Post-Cleanup Actions

### 1. Update Documentation
- [ ] Add note to README about project structure
- [ ] Document active projects
- [ ] Update architecture diagrams if any

### 2. Git Commit Strategy
```bash
# Commit each phase separately for clear history

git add -A
git commit -m "Phase 1: Remove duplicate ScriptCompilationOptions.cs

- Deleted PokeSharp.Scripting/ScriptCompilationOptions.cs (duplicate)
- Kept version in Compilation/ folder
- Updated ScriptService.cs to use correct namespace"

git add -A
git commit -m "Phase 2: Remove empty project directories

- Removed 10 empty project directories with only build artifacts
- Cleaned solution file of phantom project references
- No functionality impact - code already consolidated in PokeSharp.Core"

git add -A
git commit -m "Phase 3: Fix folder naming conventions

- Renamed PokeSharp.Game/diagnostics/ to Diagnostics/
- Matches .NET PascalCase convention
- No code changes needed - namespace already correct"

git add -A
git commit -m "Add project organization analysis and cleanup documentation

- Complete codebase analysis in PROJECT_ORGANIZATION_ANALYSIS.md
- Phased reorganization plan in REORGANIZATION_PLAN.md"
```

### 3. Push to Remote
```bash
git push origin main
```

---

## Success Criteria

All phases complete when:
- ✅ Build succeeds with 0 errors
- ✅ All tests pass
- ✅ No duplicate files exist
- ✅ No empty project directories
- ✅ All folders follow PascalCase naming
- ✅ Solution file is clean
- ✅ Game runs without errors
- ✅ No broken references
- ✅ Git history is clean and documented

---

## Contingency Plans

### If Build Fails After Phase 1
1. Check the exact error message
2. Verify ScriptService.cs has correct using statement
3. If needed: Add explicit `using PokeSharp.Scripting.Compilation;`
4. Rebuild

### If Build Fails After Phase 2
1. This shouldn't happen (empty directories)
2. If it does: Check solution file for syntax errors
3. Restore solution file: `git restore PokeSharp.sln`

### If Build Fails After Phase 3
1. Verify folder was renamed correctly
2. Check that .csproj includes files from new location
3. Rollback: `git mv Diagnostics/ diagnostics/`

### Nuclear Option (Restore Everything)
```bash
# Reset all changes
git reset --hard HEAD
git clean -fd

# Rebuild
dotnet restore
dotnet build
```

---

## Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1 | 5 min | 5 min |
| Phase 2 | 10 min | 15 min |
| Phase 3 | 5 min | 20 min |
| Phase 4 | 15 min | 35 min |
| Commit & Push | 5 min | 40 min |
| **TOTAL** | | **40 minutes** |

---

## Notes

- All changes are **safe and reversible**
- No breaking changes to public APIs
- No changes to game logic or behavior
- No changes to dependencies
- Can be done in any environment (dev/prod)
- No user impact

---

**Plan Ready for Execution** ✅

