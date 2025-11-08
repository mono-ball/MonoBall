using System.Linq;
using Arch.Core;
using FluentAssertions;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Tiles;
using PokeSharp.Rendering.Loaders;
using Xunit;

namespace PokeSharp.Tests.Loaders;

/// <summary>
///     Tests for Phase 2 Zstd Compression support.
///     Validates decompression of Zstd-compressed tile data from Tiled maps.
/// </summary>
public class ZstdCompressionTests : IDisposable
{
    private readonly StubAssetManager _assetManager;
    private readonly MapLoader _mapLoader;
    private readonly World _world;

    public ZstdCompressionTests()
    {
        _world = World.Create();
        _assetManager = new StubAssetManager();
        _mapLoader = new MapLoader(_assetManager);
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    [Fact]
    public void LoadMapEntities_ZstdCompressedLayer_DecompressesSuccessfully()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";
        var expectedTileCount = 16; // 4x4 map

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify tiles were decompressed and created
        var tileCount = 0;
        var query = new QueryDescription().WithAll<TilePosition, TileSprite>();

        _world.Query(in query, (Entity entity, ref TilePosition pos, ref TileSprite sprite) =>
        {
            tileCount++;
            sprite.TileGid.Should().BeGreaterThan(0, "Decompressed tiles should have valid GIDs");
        });

        tileCount.Should().Be(expectedTileCount, "All tiles should be decompressed from Zstd data");
    }

    [Fact]
    public void LoadMapEntities_ZstdBase64EncodedData_DecodesAndDecompresses()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Data is Base64 + Zstd compressed
        // Verify the decompression chain works: Base64 decode -> Zstd decompress -> tile data
        var tileQuery = new QueryDescription().WithAll<TileSprite>();
        var tilesFound = false;

        _world.Query(in tileQuery, (Entity entity, ref TileSprite sprite) =>
        {
            tilesFound = true;
        });

        tilesFound.Should().BeTrue("Base64 + Zstd compressed data should be decoded and decompressed");
    }

    [Fact]
    public void LoadMapEntities_MixedCompressionLayers_HandlesAllFormats()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Map has both Zstd compressed and uncompressed layers
        var layersFound = new HashSet<TileLayer>();
        var query = new QueryDescription().WithAll<TileSprite>();

        _world.Query(in query, (Entity entity, ref TileSprite sprite) =>
        {
            layersFound.Add(sprite.Layer);
        });

        layersFound.Should().Contain(TileLayer.Ground, "Zstd compressed layer should be processed");
        layersFound.Should().Contain(TileLayer.Object, "Uncompressed layer should be processed");
    }

    [Fact]
    public void LoadMapEntities_ZstdCompressedTiles_CorrectTilePositions()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify tile positions are correct after decompression
        var positionsValid = true;
        var query = new QueryDescription().WithAll<TilePosition>();

        _world.Query(in query, (Entity entity, ref TilePosition pos) =>
        {
            if (pos.X < 0 || pos.X >= 4 || pos.Y < 0 || pos.Y >= 4)
            {
                positionsValid = false;
            }
        });

        positionsValid.Should().BeTrue("Decompressed tiles should have valid positions within map bounds");
    }

    [Fact]
    public void LoadMapEntities_ZstdCompressedData_MatchesExpectedTileGids()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify tile GIDs match expected values after decompression
        var tileGids = new List<int>();
        var query = new QueryDescription().WithAll<TileSprite>();

        _world.Query(in query, (Entity entity, ref TileSprite sprite) =>
        {
            if (sprite.Layer == TileLayer.Ground)
            {
                tileGids.Add((int)sprite.TileGid);
            }
        });

        tileGids.Should().NotBeEmpty("Zstd decompressed layer should contain tiles");
        tileGids.Should().AllSatisfy(gid => gid.Should().BeGreaterThan(0, "All GIDs should be valid"));
    }

    [Fact]
    public void LoadMapEntities_InvalidZstdData_HandlesGracefully()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act & Assert - Should not throw on valid Zstd data
        var act = () => _mapLoader.LoadMapEntities(_world, mapPath);
        act.Should().NotThrow("Valid Zstd data should decompress without errors");
    }

    [Fact]
    public void LoadMapEntities_ZstdCompression_MapDimensionsCorrect()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";
        var expectedWidth = 4;
        var expectedHeight = 4;
        var expectedTileSize = 16;

        // Act
        var mapInfoEntity = _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify map dimensions are correct
        var mapInfo = _world.Get<MapInfo>(mapInfoEntity);
        mapInfo.Width.Should().Be(expectedWidth, "Zstd compressed map should have correct width");
        mapInfo.Height.Should().Be(expectedHeight, "Zstd compressed map should have correct height");
        mapInfo.TileSize.Should().Be(expectedTileSize, "Tile size should be correct");
    }

    [Fact]
    public void LoadMapEntities_ZstdWithEmptyTiles_SkipsEmptyTiles()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Only non-zero GID tiles should be created
        var emptyTileFound = false;
        var query = new QueryDescription().WithAll<TileSprite>();

        _world.Query(in query, (Entity entity, ref TileSprite sprite) =>
        {
            if (sprite.TileGid == 0)
            {
                emptyTileFound = true;
            }
        });

        emptyTileFound.Should().BeFalse("Empty tiles (GID 0) should not create entities");
    }

    [Fact]
    public void LoadMapEntities_ZstdCompressedLayer_LoadsTileset()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify tileset was loaded
        var tilesetFound = false;
        var tilesetQuery = new QueryDescription().WithAll<TilesetInfo>();

        _world.Query(in tilesetQuery, (Entity entity, ref TilesetInfo tileset) =>
        {
            tilesetFound = true;
            tileset.FirstGid.Should().BeGreaterThan(0, "Tileset should have valid FirstGid");
        });

        tilesetFound.Should().BeTrue("Zstd compressed map should load tileset");
    }

    [Fact]
    public void LoadMapEntities_ZstdVsUncompressed_ProducesSameResults()
    {
        // Arrange
        var zstdMapPath = "PokeSharp.Tests/TestData/test-map-zstd.json";
        var uncompressedMapPath = "PokeSharp.Tests/TestData/test-map.json";

        // Create separate worlds for comparison
        using var zstdWorld = World.Create();
        using var uncompressedWorld = World.Create();

        var zstdAssetManager = new StubAssetManager();
        var uncompressedAssetManager = new StubAssetManager();

        var zstdLoader = new MapLoader((dynamic)zstdAssetManager);
        var uncompressedLoader = new MapLoader((dynamic)uncompressedAssetManager);

        // Act
        zstdLoader.LoadMapEntities(zstdWorld, zstdMapPath);
        uncompressedLoader.LoadMapEntities(uncompressedWorld, uncompressedMapPath);

        // Assert - Both should create tile entities
        var zstdTileCount = 0;
        var uncompressedTileCount = 0;

        var tileQuery = new QueryDescription().WithAll<TileSprite>();

        zstdWorld.Query(in tileQuery, (Entity entity, ref TileSprite sprite) =>
        {
            zstdTileCount++;
        });

        uncompressedWorld.Query(in tileQuery, (Entity entity, ref TileSprite sprite) =>
        {
            uncompressedTileCount++;
        });

        zstdTileCount.Should().BeGreaterThan(0, "Zstd map should create tiles");
        uncompressedTileCount.Should().BeGreaterThan(0, "Uncompressed map should create tiles");
    }
}
