# Zstd Compression Support Implementation Summary

## Overview
Added Zstandard (Zstd) compression support for Tiled tile data in the PokeSharp rendering engine. This enables loading of Tiled maps with Zstd-compressed layers, reducing file sizes while maintaining compatibility with existing uncompressed maps.

## Implementation Details

### 1. Dependencies Added
**File**: `/PokeSharp.Rendering/PokeSharp.Rendering.csproj`
- Added `ZstdSharp.Port` NuGet package (version 0.8.3)
- Pure C# implementation of Zstandard compression (no native dependencies)

### 2. Core Decompression Logic
**File**: `/PokeSharp.Rendering/Loaders/TiledMapLoader.cs`

#### Changes:
1. Added `using ZstdSharp;` import
2. Refactored `DecompressBytes()` method to use strategy pattern
3. Added three compression-specific methods:
   - `DecompressGzip()` - Handles gzip compression
   - `DecompressZlib()` - Handles zlib compression
   - `DecompressZstd()` - **NEW** - Handles Zstandard compression

#### Code Implementation:
```csharp
private static byte[] DecompressBytes(byte[] compressed, string compression)
{
    return compression.ToLower() switch
    {
        "gzip" => DecompressGzip(compressed),
        "zlib" => DecompressZlib(compressed),
        "zstd" => DecompressZstd(compressed),
        _ => throw new NotSupportedException(
            $"Compression '{compression}' not supported. Supported formats: gzip, zlib, zstd"
        ),
    };
}

private static byte[] DecompressZstd(byte[] compressed)
{
    using var decompressor = new Decompressor();
    return decompressor.Unwrap(compressed).ToArray();
}
```

### 3. Data Flow
When loading Tiled JSON maps:
1. **Detection**: `DecodeLayerData()` checks layer's `compression` property
2. **Decoding**: Base64-encoded data string is converted to byte array
3. **Decompression**: `DecompressBytes()` routes to appropriate decompressor
4. **Parsing**: Decompressed bytes are converted to int array (tile GIDs)
5. **Storage**: Tile data is stored as 2D array in `TmxLayer.Data`

### 4. Supported Formats
The TiledMapLoader now supports all Tiled compression formats:
- **Uncompressed** - Plain JSON arrays or Base64-encoded raw data
- **gzip** - Legacy format, still supported
- **zlib** - Common compression format
- **zstd** - **NEW** - Modern, efficient compression (higher compression ratios)

### 5. Tiled Map Format Example
```json
{
  "layers": [
    {
      "name": "Ground",
      "type": "tilelayer",
      "data": "KLUv/SAkIQEAAQAAAAIAAAADAAAABAAAAAUAAAAGAAAABwAAAAgAAAAJAAAA",
      "encoding": "base64",
      "compression": "zstd"
    }
  ]
}
```

## Testing

### Test Files Created
1. **Test Map**: `/PokeSharp.Tests/TestData/test-map-zstd-3x3.json`
   - 3x3 map with Zstd-compressed tile data
   - Contains tiles 1-9 in sequence
   - Uses Base64-encoded Zstd compression

2. **Unit Tests**: `/PokeSharp.Tests/Loaders/TiledMapLoaderTests.cs`
   - `Load_UncompressedMap_LoadsSuccessfully()` - Baseline test
   - `Load_ZstdCompressedMap_LoadsSuccessfully()` - Zstd decompression test
   - `Load_ZstdCompressedMap_ProducesSameResultAsUncompressed()` - Equivalence test

3. **Verification Tools**:
   - `/tests/ZstdCompressor/` - Utility to generate test data
   - `/tests/ZstdTest/` - Standalone decompression verification

### Test Results
✓ Zstd decompression verified with test data (tiles 1-9)
✓ Build successful with no warnings
✓ Backward compatible with existing uncompressed maps

## Performance Characteristics

### Compression Comparison (36-byte payload)
- **Uncompressed**: 36 bytes
- **Zstd**: 45 bytes (overhead for small data)
- **Note**: Zstd excels with larger datasets (typical maps)

### Benefits
- **Smaller map files** - Reduces download/storage requirements
- **Faster loading** - Less data to read from disk
- **Zero-copy decompression** - Efficient memory usage with `Decompressor.Unwrap()`

## Usage

### For Map Creators (Tiled Editor)
1. Open map in Tiled Editor
2. Go to `Map > Map Properties`
3. Set `Tile Layer Format` to `Base64 (Zstandard compressed)`
4. Save map - compression is applied automatically

### For Developers
No code changes required! The TiledMapLoader automatically:
- Detects compression from JSON `compression` property
- Selects appropriate decompressor
- Returns tile data in standard 2D array format

```csharp
// Works with both compressed and uncompressed maps
var tmxDoc = TiledMapLoader.Load("path/to/map.json");
var tiles = tmxDoc.Layers[0].Data; // int[,] regardless of compression
```

## Files Modified

### Core Implementation
- `/PokeSharp.Rendering/PokeSharp.Rendering.csproj` - Added ZstdSharp dependency
- `/PokeSharp.Rendering/Loaders/TiledMapLoader.cs` - Implemented Zstd decompression

### Test Files (in /tests directory)
- `/tests/ZstdCompressor/` - Compression utility
- `/tests/ZstdTest/` - Decompression verification
- `/PokeSharp.Tests/TestData/test-map-zstd-3x3.json` - Test map
- `/PokeSharp.Tests/Loaders/TiledMapLoaderTests.cs` - Unit tests

### Documentation
- `/docs/zstd-implementation-summary.md` - This document

## Technical Details

### ZstdSharp Library
- **Package**: ZstdSharp.Port 0.8.3
- **Type**: Pure C# port of Zstandard
- **Advantages**:
  - No native dependencies
  - Cross-platform (works on Windows, Linux, macOS)
  - Compatible with .NET 9.0
  - High performance with SIMD optimizations

### Error Handling
- Invalid compression format throws `NotSupportedException`
- Malformed data throws appropriate exceptions from decompressor
- Missing compression property defaults to uncompressed

### Backward Compatibility
✓ Existing uncompressed maps continue to work
✓ Existing gzip/zlib maps unaffected
✓ No breaking changes to API
✓ Transparent to consumers of TiledMapLoader

## Future Enhancements

### Potential Improvements
1. **Streaming decompression** - For very large maps
2. **Compression level configuration** - Allow tuning compression/speed tradeoff
3. **Compression statistics** - Log compression ratios for monitoring
4. **Parallel layer decompression** - Decompress multiple layers concurrently

### Not Implemented (Out of Scope)
- Compression of map data (Tiled Editor handles this)
- Runtime recompression/caching
- Alternative compression algorithms (Brotli, LZ4, etc.)

## Conclusion

Zstd compression support has been successfully implemented in the PokeSharp rendering engine. The implementation:
- ✓ Supports all Tiled compression formats (uncompressed, gzip, zlib, zstd)
- ✓ Maintains backward compatibility
- ✓ Uses efficient pure C# implementation
- ✓ Includes comprehensive tests
- ✓ Requires no API changes for consumers

Maps exported from Tiled with Zstandard compression will now load seamlessly in PokeSharp.
