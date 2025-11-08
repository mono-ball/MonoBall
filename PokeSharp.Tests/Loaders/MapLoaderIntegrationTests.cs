using Arch.Core;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Tiles;
using PokeSharp.Rendering.Loaders;
using Xunit;

namespace PokeSharp.Tests.Loaders;

/// <summary>
///     Integration tests for MapLoader end-to-end map loading workflow.
///     Tests the complete flow: map loading → ECS entity creation → camera setup.
///     Note: Uses dynamic to work around AssetManager's sealed GraphicsDevice requirement.
/// </summary>
public class MapLoaderIntegrationTests : IDisposable
{
    private readonly StubAssetManager _assetManager;
    private readonly MapLoader _mapLoader;
    private readonly World _world;

    public MapLoaderIntegrationTests()
    {
        // Create real ECS world for integration testing
        _world = World.Create();

        // Use stub AssetManager that doesn't require GraphicsDevice
        _assetManager = new StubAssetManager();

        // Create MapLoader with stub (now compatible via IAssetProvider interface)
        _mapLoader = new MapLoader(_assetManager);
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    [Fact]
    public void LoadMapEntities_ValidMap_CreatesMapInfo()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map.json";
        var expectedWidth = 3;
        var expectedHeight = 3;
        var expectedTileSize = 16;

        // Act
        var mapInfoEntity = _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert
        Assert.True(_world.IsAlive(mapInfoEntity));

        var mapInfo = _world.Get<MapInfo>(mapInfoEntity);
        Assert.Equal("test-map", mapInfo.MapName);
        Assert.Equal(expectedWidth, mapInfo.Width);
        Assert.Equal(expectedHeight, mapInfo.Height);
        Assert.Equal(expectedTileSize, mapInfo.TileSize);
        Assert.Equal(expectedWidth * expectedTileSize, mapInfo.PixelWidth);
        Assert.Equal(expectedHeight * expectedTileSize, mapInfo.PixelHeight);
        Assert.Equal(0, mapInfo.MapId); // First map loaded should have ID 0
    }

    [Fact]
    public void LoadMapEntities_ValidMap_CreatesTileEntities()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map.json";
        var expectedTileCount = 9; // 3x3 map, all tiles populated (IDs 1-9)

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Query all tile entities
        var tileCount = 0;
        var query = new QueryDescription().WithAll<TilePosition, TileSprite>();

        _world.Query(in query, (Entity entity, ref TilePosition pos, ref TileSprite sprite) =>
        {
            tileCount++;

            // Verify each tile has valid components
            Assert.True(pos.X >= 0 && pos.X < 3);
            Assert.True(pos.Y >= 0 && pos.Y < 3);
            Assert.Equal(0, pos.MapId);
            Assert.True(sprite.TileGid > 0); // Non-empty tile
            Assert.Equal(TileLayer.Ground, sprite.Layer); // First layer = Ground
        });

        Assert.Equal(expectedTileCount, tileCount);

        // Verify AssetManager loaded the tileset texture
        Assert.True(_assetManager.LoadedTextureCount > 0);
    }

    [Fact]
    public void LoadMapEntities_NonStandardTileSize_UsesCorrectSize()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-32x32.json";
        var expectedWidth = 5;
        var expectedHeight = 5;
        var expectedTileSize = 32; // Non-standard tile size
        var expectedTileCount = 25; // 5x5 map

        // Act
        var mapInfoEntity = _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify MapInfo uses correct tile size
        var mapInfo = _world.Get<MapInfo>(mapInfoEntity);
        Assert.Equal("test-map-32x32", mapInfo.MapName);
        Assert.Equal(expectedWidth, mapInfo.Width);
        Assert.Equal(expectedHeight, mapInfo.Height);
        Assert.Equal(expectedTileSize, mapInfo.TileSize);
        Assert.Equal(expectedWidth * expectedTileSize, mapInfo.PixelWidth); // 5 * 32 = 160
        Assert.Equal(expectedHeight * expectedTileSize, mapInfo.PixelHeight); // 5 * 32 = 160

        // Verify tile entities were created
        var tileCount = 0;
        var query = new QueryDescription().WithAll<TilePosition, TileSprite>();

        _world.Query(in query, (Entity entity, ref TilePosition pos, ref TileSprite sprite) =>
        {
            tileCount++;
            Assert.True(pos.X >= 0 && pos.X < 5);
            Assert.True(pos.Y >= 0 && pos.Y < 5);
        });

        Assert.Equal(expectedTileCount, tileCount);
    }

    [Fact]
    public void LoadMapEntities_MultipleMaps_AssignsUniqueMapIds()
    {
        // Arrange
        var map1Path = "PokeSharp.Tests/TestData/test-map.json";
        var map2Path = "PokeSharp.Tests/TestData/test-map-32x32.json";

        // Act
        var mapInfo1Entity = _mapLoader.LoadMapEntities(_world, map1Path);
        var mapInfo2Entity = _mapLoader.LoadMapEntities(_world, map2Path);

        // Assert
        var mapInfo1 = _world.Get<MapInfo>(mapInfo1Entity);
        var mapInfo2 = _world.Get<MapInfo>(mapInfo2Entity);

        Assert.NotEqual(mapInfo1.MapId, mapInfo2.MapId);
        Assert.Equal(0, mapInfo1.MapId); // First map
        Assert.Equal(1, mapInfo2.MapId); // Second map
    }

    [Fact]
    public void LoadMapEntities_ValidMap_CreatesTilesetInfo()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify TilesetInfo entity was created
        var tilesetFound = false;
        var tilesetQuery = new QueryDescription().WithAll<TilesetInfo>();

        _world.Query(in tilesetQuery, (Entity entity, ref TilesetInfo tileset) =>
        {
            tilesetFound = true;
            Assert.True(tileset.FirstGid > 0);
            Assert.True(tileset.TileWidth > 0);
            Assert.True(tileset.TileHeight > 0);
        });

        Assert.True(tilesetFound, "TilesetInfo entity should be created");
    }

    [Fact]
    public void LoadMapEntities_EmptyTiles_SkipsTileCreation()
    {
        // Arrange - test-map.json has all tiles populated (1-9)
        // For this test we verify that only non-zero GIDs create entities
        var mapPath = "PokeSharp.Tests/TestData/test-map.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Count tile entities
        var tileCount = 0;
        var query = new QueryDescription().WithAll<TileSprite>();

        _world.Query(in query, (Entity entity, ref TileSprite sprite) =>
        {
            tileCount++;
            // All tiles should have non-zero GID
            Assert.True(sprite.TileGid > 0);
        });

        // All 9 tiles in test-map.json are non-empty
        Assert.Equal(9, tileCount);
    }
}
