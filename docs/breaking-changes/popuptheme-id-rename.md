# Breaking Change: PopupTheme.Id Renamed to ThemeId

## Summary
The `PopupTheme.Id` property has been renamed to `ThemeId` for consistency with other entity classes in the codebase.

## Affected Component
- **Entity**: `MonoBallFramework.Game.GameData.Entities.PopupTheme`
- **Property**: `Id` → `ThemeId`
- **Type**: `GameThemeId` (unchanged)

## Rationale
The codebase follows a naming pattern where entity primary keys are named `{Type}Id`:
- `MapEntity.MapId`
- `FontEntity.FontId`
- `SpriteEntity.SpriteId`
- `MapSection.MapSectionId`

Previously, `PopupTheme` used just `Id`, which was inconsistent with this pattern.

## Changes Made

### 1. Entity Property Rename
**File**: `/MonoBallFramework.Game/GameData/Entities/PopupTheme.cs`

```csharp
// Before
public GameThemeId Id { get; set; } = null!;

// After
public GameThemeId ThemeId { get; set; } = null!;
```

### 2. Usage Updates

**File**: `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`
```csharp
// Line 502
_logger.LogPopupThemeLoaded(theme.ThemeId, theme.Name);
```

**File**: `/MonoBallFramework.Game/GameData/Services/MapPopupDataService.cs`
```csharp
// Line 132 - LINQ query
.PopupThemes.FirstOrDefaultAsync(t => t.ThemeId == themeId, ct);

// Line 157 - Cache dictionary key
_themeCache[theme.ThemeId] = theme;

// Line 70 - Debug logging
string.Join(", ", themes.Select(t => $"{t.ThemeId} ({t.Name})"))
```

## Database Impact

### ⚠️ BREAKING CHANGE - Database Schema
This change affects the database schema. The primary key column name will change from `Id` to `ThemeId`.

**Migration Required**: Yes

**Impact**:
- Existing databases will need migration
- Foreign key references in `MapSection.ThemeId` remain unchanged (already used `ThemeId`)
- No data loss expected - only column rename

### Recommended Migration Approach

For SQLite (development):
```sql
-- EF Core will generate appropriate migration
-- Or manual rename:
ALTER TABLE PopupThemes RENAME COLUMN Id TO ThemeId;
```

For SQL Server (production):
```sql
EXEC sp_rename 'PopupThemes.Id', 'ThemeId', 'COLUMN';
```

## Verification

### Files Changed
- ✅ `PopupTheme.cs` - Property renamed
- ✅ `GameDataLoader.cs` - Logger call updated
- ✅ `MapPopupDataService.cs` - LINQ query updated (3 locations)

### Foreign Key Compatibility
The `MapSection` entity already uses `ThemeId` as the foreign key property:
```csharp
[ForeignKey(nameof(ThemeId))]
public PopupTheme? Theme { get; set; }
```

No changes required in `MapSection.cs`.

### Build Status
- ✅ No compilation errors related to this change
- ⚠️ Note: Unrelated build error exists for missing `GameplayConfig.cs`

## Migration Checklist

- [ ] Generate EF Core migration for PopupTheme schema change
- [ ] Test migration on development database
- [ ] Backup production database before migration
- [ ] Apply migration to production
- [ ] Verify data integrity after migration
- [ ] Update any external tools/scripts that reference `PopupTheme.Id`

## API Impact
This change only affects internal code. No public API changes unless `PopupTheme` is exposed in DTOs or API endpoints.

## Date
2025-12-16

## Related Entities
- `MapSection` - Foreign key relationship maintained
- All other entities follow `{Type}Id` pattern consistently now
