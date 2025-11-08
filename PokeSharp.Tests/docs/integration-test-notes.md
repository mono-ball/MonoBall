# MapLoader Integration Test Notes

## Created Tests

The following integration tests have been created in `/PokeSharp.Tests/Loaders/MapLoaderIntegrationTests.cs`:

1. **LoadMapEntities_ValidMap_CreatesMapInfo** - Verifies MapInfo entity creation with correct dimensions
2. **LoadMapEntities_ValidMap_CreatesTileEntities** - Verifies tile entities are created from map data
3. **LoadMapEntities_NonStandardTileSize_UsesCorrectSize** - Tests 32x32 tile support
4. **LoadMapEntities_MultipleMaps_AssignsUniqueMapIds** - Verifies unique map ID assignment
5. **LoadMapEntities_ValidMap_CreatesTilesetInfo** - Verifies TilesetInfo entity creation
6. **LoadMapEntities_EmptyTiles_SkipsTileCreation** - Verifies empty tiles (GID=0) are skipped

## Test Data Created

- `/PokeSharp.Tests/TestData/test-map.json` - 3x3 map with 16x16 tiles (existing)
- `/PokeSharp.Tests/TestData/test-map-32x32.json` - 5x5 map with 32x32 tiles (new)

## Current Blocker

**Cannot run tests due to AssetManager dependency on GraphicsDevice.**

### Problem

`AssetManager` requires a `GraphicsDevice` in its constructor:
```csharp
public AssetManager(GraphicsDevice graphicsDevice, string assetRoot, ILogger<AssetManager>? logger)
{
    _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
}
```

`GraphicsDevice` cannot be instantiated in headless test environments (CI/CD, unit tests).

### Attempted Solutions

1. **Moq** - Failed: AssetManager methods are not virtual
2. **NSubstitute** - Not available in project
3. **Inheritance with fake GraphicsDevice** - Failed: GraphicsDevice.Setup() requires display adapter
4. **Reflection** - Failed: Constructor validates before body executes
5. **Dynamic** - Failed: Runtime type checking still enforced

### Recommended Solution

**Extract IAssetProvider interface** from AssetManager:

```csharp
public interface IAssetProvider
{
    void LoadTexture(string id, string relativePath);
    bool HasTexture(string id);
    int LoadedTextureCount { get; }
}

public class AssetManager : IAssetProvider
{
    // Existing implementation
}
```

Then update `MapLoader` to accept `IAssetProvider`:

```csharp
public class MapLoader(
    IAssetProvider assetProvider,
    IEntityFactoryService? entityFactory = null,
    ILogger<MapLoader>? logger = null)
```

This allows test doubles without GraphicsDevice dependency.

### Test Coverage When Unblocked

Once the interface is extracted, these tests will verify:

- ✅ MapInfo entity created with correct Width, Height, TileSize
- ✅ PixelWidth and PixelHeight calculated correctly
- ✅ Tile entities created for all non-empty tiles
- ✅ TilePosition and TileSprite components attached correctly
- ✅ Non-standard tile sizes (16x16, 32x32) handled properly
- ✅ Multiple maps assigned unique MapIds
- ✅ TilesetInfo entity created
- ✅ Empty tiles (GID=0) skipped

### Running Tests Locally

If you have a display/GPU available:

```bash
dotnet test --filter "FullyQualifiedName~MapLoaderIntegrationTests"
```

Tests will pass if GraphicsDevice can be created in your environment.

## Files

- **Tests**: `/PokeSharp.Tests/Loaders/MapLoaderIntegrationTests.cs`
- **Test Data**: `/PokeSharp.Tests/TestData/test-map.json`, `test-map-32x32.json`
- **Stub (unused)**: `/PokeSharp.Tests/Loaders/StubAssetManager.cs`
- **Fixture (unused)**: `/PokeSharp.Tests/Fixtures/TestAssetManager.cs`

## Next Steps

1. Extract `IAssetProvider` interface from `AssetManager`
2. Update `MapLoader` constructor to accept `IAssetProvider`
3. Create test implementation of `IAssetProvider`
4. Run integration tests
5. Verify all tests pass
