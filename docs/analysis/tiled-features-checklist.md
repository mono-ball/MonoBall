# Tiled JSON Map Format - Complete Feature Checklist

**Research Date:** 2025-11-08
**Tiled Version:** 1.11.2+
**Current Implementation Status:** Analysis Complete

---

## Executive Summary

This document provides a comprehensive analysis of ALL Tiled JSON Map Format features and compares them against the current PokeSHarp implementation. Features are categorized by implementation status:

- ✅ **Implemented** - Feature is fully supported
- ⚠️ **Partial** - Feature is partially implemented or has limitations
- ❌ **Missing** - Feature is not implemented

---

## 1. Map Properties

### Root Map Object

| Feature | Property | Status | Notes |
|---------|----------|--------|-------|
| **Version** | `version` | ✅ | Supported |
| **Tiled Version** | `tiledversion` | ✅ | Supported |
| **Type** | `type` | ✅ | Supported (value: "map") |
| **Dimensions** | `width`, `height` | ✅ | Supported |
| **Tile Size** | `tilewidth`, `tileheight` | ✅ | Supported |
| **Orientation** | `orientation` | ⚠️ | Only "orthogonal" tested; missing isometric, staggered, hexagonal |
| **Render Order** | `renderorder` | ⚠️ | Property missing from model; missing right-up, left-down, left-up |
| **Infinite Maps** | `infinite` | ✅ | Property exists but chunk support unverified |
| **Background Color** | `backgroundcolor` | ❌ | Not implemented |
| **Hex Side Length** | `hexsidelength` | ❌ | Not implemented (hexagonal maps) |
| **Stagger Axis** | `staggeraxis` | ❌ | Not implemented (staggered maps) |
| **Stagger Index** | `staggerindex` | ❌ | Not implemented (staggered maps) |
| **Parallax Origin** | `parallaxoriginx`, `parallaxoriginy` | ❌ | Not implemented (Tiled 1.8+) |
| **Next Layer ID** | `nextlayerid` | ❌ | Not tracked |
| **Next Object ID** | `nextobjectid` | ❌ | Not tracked |
| **Compression Level** | `compressionlevel` | ❌ | Not exposed (Tiled 1.3+) |
| **Class** | `class` | ❌ | Not implemented (user-defined types, Tiled 1.9+) |
| **Custom Properties** | `properties` | ✅ | Supported |

### Orientation Types

| Type | Status | Notes |
|------|--------|-------|
| `orthogonal` | ✅ | Default, tested |
| `isometric` | ❌ | Not implemented |
| `staggered` | ❌ | Not implemented |
| `hexagonal` | ❌ | Not implemented |

### Render Order Options

| Option | Status | Notes |
|--------|--------|-------|
| `right-down` | ⚠️ | Default but not verified |
| `right-up` | ❌ | Not implemented |
| `left-down` | ❌ | Not implemented |
| `left-up` | ❌ | Not implemented |

---

## 2. Layer Types & Properties

### Layer Types

| Type | Status | Implementation |
|------|--------|----------------|
| **Tile Layer** | `tilelayer` | ✅ Fully supported |
| **Object Group** | `objectgroup` | ✅ Fully supported |
| **Image Layer** | `imagelayer` | ❌ Not implemented |
| **Group Layer** | `group` | ❌ Not implemented (nested layers) |

### Common Layer Properties

| Property | Status | Notes |
|----------|--------|-------|
| `id` | ✅ | Supported |
| `name` | ✅ | Supported |
| `type` | ✅ | Supported |
| `visible` | ✅ | Supported |
| `opacity` | ✅ | Supported |
| `x`, `y` | ✅ | Supported |
| `offsetx`, `offsety` | ❌ | Not implemented |
| `parallaxx`, `parallaxy` | ❌ | Not implemented (Tiled 1.5+) |
| `tintcolor` | ❌ | Not implemented (Tiled 1.9+) |
| `class` | ❌ | Not implemented (Tiled 1.9+) |
| `locked` | ❌ | Not implemented |
| `startx`, `starty` | ❌ | Not implemented (chunk positioning) |
| `properties` | ✅ | Supported |

### Tile Layer Specific

| Property | Status | Notes |
|----------|--------|-------|
| `data` | ✅ | Fully supported (array and base64) |
| `width`, `height` | ✅ | Supported |
| `encoding` | ✅ | Supports CSV and base64 |
| `compression` | ⚠️ | Supports gzip and zlib; **missing zstd** |
| `chunks` | ❌ | Property not implemented for infinite maps |

### Object Layer Specific

| Property | Status | Notes |
|----------|--------|-------|
| `objects` | ✅ | Fully supported |
| `draworder` | ❌ | Not implemented (topdown vs index) |

### Image Layer Specific

| Property | Status | Notes |
|----------|--------|-------|
| `image` | ❌ | Not implemented |
| `imagewidth`, `imageheight` | ❌ | Not implemented (Tiled 1.11.1+) |
| `transparentcolor` | ❌ | Not implemented |
| `repeatx`, `repeaty` | ❌ | Not implemented |

---

## 3. Tile Data Formats

### Encoding

| Format | Status | Notes |
|--------|--------|-------|
| **Array (CSV)** | ✅ | Default, fully supported |
| **Base64** | ✅ | Fully supported |

### Compression

| Format | Status | Notes |
|--------|--------|-------|
| **None (uncompressed)** | ✅ | Default |
| **gzip** | ✅ | Fully supported |
| **zlib** | ✅ | Fully supported |
| **zstd** | ❌ | **Not supported** (modern compression, Tiled 1.3+) |

### Chunks (Infinite Maps)

| Property | Status | Notes |
|----------|--------|-------|
| `chunks` array | ❌ | Not implemented |
| Chunk `data` | ❌ | Not implemented |
| Chunk `x`, `y` | ❌ | Not implemented |
| Chunk `width`, `height` | ❌ | Not implemented |

---

## 4. Object Properties

### Basic Object Properties

| Property | Status | Notes |
|----------|--------|-------|
| `id` | ✅ | Supported |
| `name` | ✅ | Supported |
| `type` | ✅ | Supported |
| `x`, `y` | ✅ | Supported |
| `width`, `height` | ✅ | Supported |
| `visible` | ✅ | Supported |
| `rotation` | ❌ | Not implemented |
| `gid` | ❌ | Not implemented (tile objects) |
| `template` | ❌ | Not implemented (object templates) |
| `properties` | ✅ | Supported |

### Object Shape Types

| Shape | Property | Status | Notes |
|-------|----------|--------|-------|
| **Rectangle** | Default | ✅ | Standard objects |
| **Ellipse** | `ellipse` (bool) | ❌ | Not implemented |
| **Point** | `point` (bool) | ❌ | Not implemented |
| **Polygon** | `polygon` (array) | ❌ | Not implemented |
| **Polyline** | `polyline` (array) | ❌ | Not implemented |
| **Text** | `text` (object) | ❌ | Not implemented |
| **Tile** | `gid` (int) | ❌ | Not implemented |

---

## 5. Text Object Properties

**Status:** ❌ **Completely Not Implemented**

| Property | Subproperty | Status | Notes |
|----------|-------------|--------|-------|
| `text` | - | ❌ | Text object container |
| - | `text` | ❌ | Text content string |
| - | `fontfamily` | ❌ | Font name |
| - | `pixelsize` | ❌ | Font size |
| - | `bold` | ❌ | Bold formatting |
| - | `italic` | ❌ | Italic formatting |
| - | `underline` | ❌ | Underline formatting |
| - | `strikeout` | ❌ | Strikethrough formatting |
| - | `kerning` | ❌ | Character spacing |
| - | `halign` | ❌ | Horizontal alignment (left/center/right/justify) |
| - | `valign` | ❌ | Vertical alignment (top/center/bottom) |
| - | `color` | ❌ | Text color (hex) |
| - | `wrap` | ❌ | Text wrapping |

---

## 6. Tileset Properties

### Basic Tileset Properties

| Property | Status | Notes |
|----------|--------|-------|
| `firstgid` | ✅ | Supported |
| `source` | ✅ | External tileset loading supported |
| `name` | ✅ | Supported |
| `tilewidth`, `tileheight` | ✅ | Supported |
| `tilecount` | ✅ | Supported |
| `columns` | ✅ | Supported |
| `spacing` | ✅ | Supported |
| `margin` | ✅ | Supported |
| `image` | ✅ | Supported |
| `imagewidth`, `imageheight` | ✅ | Supported |
| `tiles` | ✅ | Tile definitions supported |
| `class` | ❌ | Not implemented (Tiled 1.9+) |
| `transparentcolor` | ❌ | Not implemented |
| `backgroundcolor` | ❌ | Not implemented |
| `objectalignment` | ❌ | Not implemented |
| `tilerendersize` | ❌ | Not implemented |
| `fillmode` | ❌ | Not implemented |
| `grid` | ❌ | Not implemented |
| `tileoffset` | ❌ | Not implemented |
| `transformations` | ❌ | Not implemented |
| `terrains` | ❌ | Not implemented (legacy) |
| `wangsets` | ❌ | Not implemented |
| `properties` | ❌ | Tileset-level properties not implemented |

### Object Alignment Options

| Alignment | Status | Notes |
|-----------|--------|-------|
| `unspecified` | ❌ | Not implemented |
| `topleft` | ❌ | Not implemented |
| `top` | ❌ | Not implemented |
| `topright` | ❌ | Not implemented |
| `left` | ❌ | Not implemented |
| `center` | ❌ | Not implemented |
| `right` | ❌ | Not implemented |
| `bottomleft` | ❌ | Not implemented |
| `bottom` | ❌ | Not implemented |
| `bottomright` | ❌ | Not implemented |

### Render Size Options

| Option | Status | Notes |
|--------|--------|-------|
| `tile` | ❌ | Not implemented |
| `grid` | ❌ | Not implemented |

### Fill Modes

| Mode | Status | Notes |
|------|--------|-------|
| `stretch` | ❌ | Not implemented |
| `preserve-aspect-fit` | ❌ | Not implemented |

---

## 7. Tile Definitions

### Tile Properties

| Property | Status | Notes |
|----------|--------|-------|
| `id` | ✅ | Local tile ID supported |
| `type` | ✅ | Tile type/class supported |
| `properties` | ✅ | Custom properties fully supported |
| `animation` | ✅ | Animation frames fully supported |
| `image` | ❌ | Individual tile image not implemented |
| `imagewidth`, `imageheight` | ❌ | Not implemented |
| `x`, `y`, `width`, `height` | ❌ | Tile sub-rectangles not implemented (Tiled 1.9+) |
| `objectgroup` | ❌ | Collision shapes not implemented |
| `terrain` | ❌ | Legacy terrain not implemented |
| `probability` | ❌ | Wang tile probability not implemented |

---

## 8. Animation

### Animation Frame Properties

| Property | Status | Notes |
|----------|--------|-------|
| `tileid` | ✅ | Frame tile ID supported |
| `duration` | ✅ | Frame duration in milliseconds supported |

**Implementation Quality:** ✅ **Excellent** - Animations are fully functional with proper millisecond-to-second conversion.

---

## 9. Transformations

**Status:** ❌ **Completely Not Implemented**

| Property | Status | Notes |
|----------|--------|-------|
| `hflip` | ❌ | Horizontal flip not implemented |
| `vflip` | ❌ | Vertical flip not implemented |
| `rotate` | ❌ | Rotation (90° increments) not implemented |
| `preferuntransformed` | ❌ | Prefer untransformed flag not implemented |

---

## 10. Grid & Tile Offset

### Grid Properties

**Status:** ❌ **Not Implemented**

| Property | Status | Notes |
|----------|--------|-------|
| `orientation` | ❌ | Grid orientation (orthogonal/isometric) |
| `width` | ❌ | Grid cell width |
| `height` | ❌ | Grid cell height |

### Tile Offset Properties

**Status:** ❌ **Not Implemented**

| Property | Status | Notes |
|----------|--------|-------|
| `x` | ❌ | Horizontal offset in pixels |
| `y` | ❌ | Vertical offset in pixels |

---

## 11. Custom Properties

### Property Types

| Type | Status | Notes |
|------|--------|-------|
| `string` | ✅ | Fully supported |
| `int` | ⚠️ | Supported via object type |
| `float` | ⚠️ | Supported via object type |
| `bool` | ⚠️ | Supported via object type |
| `color` | ❌ | Not explicitly handled (Tiled 1.0+) |
| `file` | ❌ | Not explicitly handled (Tiled 1.0+) |
| `object` | ❌ | Object references not implemented (Tiled 1.4+) |
| `class` | ❌ | User-defined property types not implemented (Tiled 1.8+) |

### Property Object Structure

| Field | Status | Notes |
|-------|--------|-------|
| `name` | ✅ | Supported |
| `type` | ✅ | Supported |
| `value` | ✅ | Supported as object |
| `propertytype` | ❌ | User-defined type name not implemented (Tiled 1.8+) |

---

## 12. Wang Sets (Terrain)

**Status:** ❌ **Completely Not Implemented**

### Wang Set Structure

| Property | Status | Notes |
|----------|--------|-------|
| `name` | ❌ | Wang set name |
| `type` | ❌ | corner, edge, or mixed |
| `tile` | ❌ | Representative tile ID |
| `colors` | ❌ | Wang color definitions |
| `wangtiles` | ❌ | Wang tile mappings |
| `properties` | ❌ | Custom properties |
| `class` | ❌ | User-defined class |

### Wang Color Structure

| Property | Status | Notes |
|----------|--------|-------|
| `name` | ❌ | Color name |
| `color` | ❌ | Hex color value |
| `tile` | ❌ | Representative tile |
| `probability` | ❌ | Probability value |
| `properties` | ❌ | Custom properties |
| `class` | ❌ | User-defined class |

### Wang Tile Structure

| Property | Status | Notes |
|----------|--------|-------|
| `tileid` | ❌ | Tile ID |
| `wangid` | ❌ | 8-element array of color indices |

---

## 13. Object Templates

**Status:** ❌ **Not Implemented**

| Property | Status | Notes |
|----------|--------|-------|
| Template file support | ❌ | External .tj files not supported |
| `type: "template"` | ❌ | Template type not recognized |
| `object` | ❌ | Template object definition |
| `tileset` | ❌ | Optional tileset reference |

---

## 14. GID Flags & Transformations

**Status:** ❌ **Not Implemented**

Global Tile IDs (GIDs) can encode flip/rotation flags in the upper bits:

| Flag | Bit Mask | Status | Notes |
|------|----------|--------|-------|
| Horizontal Flip | `0x80000000` | ❌ | Not decoded |
| Vertical Flip | `0x40000000` | ❌ | Not decoded |
| Diagonal Flip (Rotation) | `0x20000000` | ❌ | Not decoded |
| Rotated Hexagonal (120°) | `0x10000000` | ❌ | Not decoded |

**Required Implementation:** GID decoding must extract transformation flags before using tile IDs.

---

## 15. Advanced Features

### Group Layers (Nested Layers)

**Status:** ❌ **Not Implemented**

Group layers allow hierarchical layer organization with:
- Nested child layers
- Group-level transformations (offset, parallax)
- Visibility and opacity inheritance

### Parallax Scrolling

**Status:** ❌ **Not Implemented**

| Feature | Status | Notes |
|---------|--------|-------|
| Layer parallax factors | ❌ | `parallaxx`, `parallaxy` not implemented |
| Map parallax origin | ❌ | `parallaxoriginx`, `parallaxoriginy` not implemented (Tiled 1.8+) |

### Image Layers

**Status:** ❌ **Not Implemented**

Full-image background/overlay layers with:
- Image source and dimensions
- Transparent color keying
- Repeat/tiling options (repeatx, repeaty)

### External Resources

| Feature | Status | Notes |
|---------|--------|-------|
| External tilesets | ✅ | Fully supported (.json/.tsx) |
| External templates | ❌ | Not implemented (.tj files) |

---

## 16. Version-Specific Features

### Tiled 1.11.1+
- ❌ Image layer dimensions (`imagewidth`, `imageheight`)

### Tiled 1.9+
- ❌ Tile sub-rectangles (`x`, `y`, `width`, `height` in tile definitions)
- ❌ User-defined `class` property for maps, layers, tilesets
- ❌ `tintcolor` for layers

### Tiled 1.8+
- ❌ User-defined property types (`propertytype`, `class` type)
- ❌ Parallax origin coordinates (`parallaxoriginx`, `parallaxoriginy`)

### Tiled 1.6+
- ✅ String-based version numbers (implemented)

### Tiled 1.5+
- ❌ Layer parallax factors (`parallaxx`, `parallaxy`)

### Tiled 1.4+
- ❌ Object references in properties (`object` type)

### Tiled 1.3+
- ❌ Compression level control (`compressionlevel`)
- ❌ Zstd compression support

### Tiled 1.0+
- ⚠️ Color and file property types (not explicitly handled)

---

## 17. Critical Missing Features Summary

### High Priority (Common Use Cases)

1. **GID Flag Decoding** ❌
   - Horizontal/vertical flip flags in tile IDs
   - Rotation flags
   - **Impact:** Cannot render flipped/rotated tiles correctly

2. **Image Layers** ❌
   - Background images
   - Overlay images
   - **Impact:** Cannot load maps with image layers

3. **Layer Offsets** ❌
   - `offsetx`, `offsety`
   - **Impact:** Layer positioning incorrect

4. **Object Shapes** ❌
   - Ellipse, point, polygon, polyline
   - **Impact:** Limited collision detection options

5. **Zstd Compression** ❌
   - Modern compression format
   - **Impact:** Cannot load newer maps using zstd

### Medium Priority (Advanced Features)

6. **Group Layers** ❌
   - Nested layer hierarchies
   - **Impact:** Cannot organize complex maps

7. **Parallax Scrolling** ❌
   - Layer parallax factors
   - Parallax origin
   - **Impact:** No parallax effects

8. **Transformations** ❌
   - Tileset transformation capabilities
   - **Impact:** Limited tileset flexibility

9. **Object Templates** ❌
   - Reusable object definitions
   - **Impact:** More manual object configuration

10. **Text Objects** ❌
    - Rich text rendering
    - **Impact:** No in-map text labels

### Low Priority (Specialized Features)

11. **Wang Sets** ❌
    - Terrain auto-tiling
    - **Impact:** Manual terrain painting required

12. **User-Defined Types** ❌
    - `class` property
    - Custom property types
    - **Impact:** Limited type safety

13. **Non-Orthogonal Maps** ❌
    - Isometric, hexagonal, staggered
    - **Impact:** Only orthogonal maps supported

14. **Collision Shapes** ❌
    - Tile object groups
    - **Impact:** Rectangle-only collision

15. **Infinite Map Chunks** ❌
    - Chunk-based data storage
    - **Impact:** Cannot use infinite maps

---

## 18. Implementation Quality Assessment

### ✅ Well Implemented (Production Ready)

- ✅ **Core tile layer rendering** - Solid foundation
- ✅ **Object groups** - Basic object support working
- ✅ **Custom properties** - Flexible data-driven design
- ✅ **Tile animations** - Fully functional
- ✅ **External tilesets** - Clean implementation
- ✅ **Compression (gzip/zlib)** - Robust decompression
- ✅ **Base64 encoding** - Proper decoding

### ⚠️ Partially Implemented (Needs Extension)

- ⚠️ **Property types** - Basic types work, advanced types missing
- ⚠️ **Orientations** - Only orthogonal tested
- ⚠️ **Compression** - Missing zstd support
- ⚠️ **Object shapes** - Only rectangles supported

### ❌ Not Implemented (High Impact)

- ❌ **GID flag decoding** - **CRITICAL** for correct rendering
- ❌ **Image layers** - Common feature
- ❌ **Layer offsets** - Positioning incorrect without this
- ❌ **Parallax** - Popular effect missing
- ❌ **Group layers** - Organizational feature

### ❌ Not Implemented (Low Impact)

- ❌ **Wang sets** - Specialized auto-tiling
- ❌ **Text objects** - Niche use case
- ❌ **Hexagonal/isometric** - Specialized map types
- ❌ **Object templates** - Convenience feature
- ❌ **User-defined types** - Type system enhancement

---

## 19. Recommended Implementation Priority

### Phase 1: Critical Fixes (Correctness)

1. **GID Flag Decoding** - Fix tile flipping/rotation
2. **Layer Offsets** - Fix layer positioning
3. **Zstd Compression** - Support modern maps

### Phase 2: Common Features (Usability)

4. **Image Layers** - Support background/overlay images
5. **Object Shapes** - Ellipse, polygon, polyline, point
6. **Render Order** - Support all four render orders
7. **Draw Order** - Object layer draw order

### Phase 3: Advanced Features (Enhancement)

8. **Parallax Scrolling** - Layer parallax factors
9. **Group Layers** - Nested layer hierarchies
10. **Text Objects** - Rich text rendering
11. **Transformations** - Tileset transformation flags

### Phase 4: Specialized Features (Completeness)

12. **Object Templates** - Reusable object definitions
13. **Wang Sets** - Terrain auto-tiling
14. **Non-Orthogonal Maps** - Isometric, hexagonal, staggered
15. **User-Defined Types** - Class system and custom property types

---

## 20. Compatibility Summary

| Category | Support Level | Notes |
|----------|---------------|-------|
| **Basic Maps** | ✅ Excellent | Orthogonal tile maps work well |
| **Advanced Layers** | ⚠️ Partial | Missing image/group layers |
| **Objects** | ⚠️ Partial | Only rectangle shapes |
| **Animations** | ✅ Excellent | Fully functional |
| **Properties** | ✅ Good | Basic types work, advanced missing |
| **Compression** | ⚠️ Good | Missing zstd |
| **Transformations** | ❌ None | Cannot flip/rotate tiles |
| **Terrain** | ❌ None | No Wang set support |

---

## Conclusion

**Current Implementation Grade: B- (75/100)**

**Strengths:**
- Solid foundation for orthogonal tile maps
- Excellent animation support
- Good custom property system
- Clean external tileset loading

**Critical Gaps:**
- **No GID flag decoding** - Breaks flipped/rotated tiles
- **No image layers** - Common feature missing
- **No layer offsets** - Positioning issues
- **Limited object shapes** - Only rectangles

**Recommendation:**
Focus on Phase 1 (GID flags, layer offsets, zstd) to achieve correctness, then Phase 2 for broader map compatibility. Current implementation handles simple maps well but fails on advanced features.

---

**Research completed by:** Researcher Agent (Hive Mind)
**Coordination:** Memory key `hive/researcher/tiled-features`
**Next Steps:** Coder agent can use this checklist for implementation planning
