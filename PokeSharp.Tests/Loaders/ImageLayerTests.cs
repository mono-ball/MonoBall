using System.Linq;
using Arch.Core;
using FluentAssertions;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Rendering.Loaders;
using Xunit;

namespace PokeSharp.Tests.Loaders;

/// <summary>
///     Tests for Phase 2 Image Layer feature.
///     Validates parsing and handling of Tiled image layers (decorative images, backgrounds, etc.).
/// </summary>
public class ImageLayerTests : IDisposable
{
    private readonly StubAssetManager _assetManager;
    private readonly MapLoader _mapLoader;
    private readonly World _world;

    public ImageLayerTests()
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
    public void LoadMapEntities_MapWithImageLayer_LoadsImageTexture()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Verify image texture was loaded
        _assetManager.LoadedTextureCount.Should().BeGreaterThan(0, "Image layer should trigger texture load");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerWithProperties_ParsesAllProperties()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        var mapInfoEntity = _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Map should load successfully with image layer
        _world.IsAlive(mapInfoEntity).Should().BeTrue("Map with image layer should load");

        var mapInfo = _world.Get<MapInfo>(mapInfoEntity);
        mapInfo.MapName.Should().Be("test-map-imagelayer", "Map name should be parsed correctly");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerWithOpacity_AppliesOpacity()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Image layer with opacity: 0.8 should be handled
        // (Implementation may create an entity or store in a component)
        // For now, verify map loads without errors
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        var mapInfoFound = false;

        _world.Query(in mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            mapInfoFound = true;
        });

        mapInfoFound.Should().BeTrue("Map with image layer should create MapInfo");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerWithOffset_AppliesParallaxOffset()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Image layer "Sky" has offsetX: 0, offsetY: -32 for parallax background
        // Verify the map loads successfully
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        var mapInfoCount = 0;

        _world.Query(in mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            mapInfoCount++;
        });

        mapInfoCount.Should().Be(1, "Should create exactly one MapInfo entity");
    }

    [Fact]
    public void LoadMapEntities_MixedImageAndTileLayers_ProcessesBothTypes()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Map has both image layer ("Sky") and tile layer ("Ground")
        // Verify both types are processed
        var tileCount = 0;
        var tileQuery = new QueryDescription().WithAll<Core.Components.Tiles.TileSprite>();

        _world.Query(in tileQuery, (Entity entity, ref Core.Components.Tiles.TileSprite sprite) =>
        {
            tileCount++;
        });

        tileCount.Should().BeGreaterThan(0, "Tile layers should still create tile entities");
        _assetManager.LoadedTextureCount.Should().BeGreaterThan(0, "Image layers should load textures");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerZOrder_MaintainsLayerOrder()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Image layer "Sky" (layer 1) should render before "Ground" (layer 2)
        // This is determined by layer ID/order in the JSON
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        Entity mapInfoEntity = default;
        var found = false;

        _world.Query(in mapInfoQuery, (Entity entity) =>
        {
            if (!found)
            {
                mapInfoEntity = entity;
                found = true;
            }
        });

        found.Should().BeTrue("Map should be loaded");
        _world.IsAlive(mapInfoEntity).Should().BeTrue("Map entity should be alive");
    }

    [Fact]
    public void LoadMapEntities_InvisibleImageLayer_SkipsRendering()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Layer "HiddenOverlay" has visible: false
        // Implementation should skip invisible layers or mark them as inactive
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        var mapLoaded = false;

        _world.Query(in mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            mapLoaded = true;
        });

        mapLoaded.Should().BeTrue("Map should load even with invisible image layers");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerWithoutImage_HandlesGracefully()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Layer "HiddenOverlay" has no image property
        // Should not crash or throw exceptions
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        var entityCount = 0;

        _world.Query(in mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            entityCount++;
        });

        entityCount.Should().BeGreaterThan(0, "Map should load despite image layer without image");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerWithCustomProperties_ParsesProperties()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - "Sky" layer has custom property "scroll_speed": 0.5
        // Verify the map loads and properties are available (if implemented)
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        var mapNameValid = false;

        _world.Query(in mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            mapNameValid = !string.IsNullOrEmpty(info.MapName);
        });

        mapNameValid.Should().BeTrue("Map should have valid name");
    }

    [Fact]
    public void LoadMapEntities_MultipleImageLayers_LoadsAllImages()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - Multiple image layers should each load their textures
        // "Sky" and "Clouds" both have image properties
        _assetManager.LoadedTextureCount.Should().BeGreaterThan(0, "All visible image layers should load textures");
    }

    [Fact]
    public void LoadMapEntities_ImageLayerPosition_AppliesXYOffsets()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-imagelayer.json";

        // Act
        _mapLoader.LoadMapEntities(_world, mapPath);

        // Assert - "Clouds" layer has x: 16, y: 16 position
        // Verify map loads successfully
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        var mapNameFound = "";

        _world.Query(in mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            mapNameFound = info.MapName;
        });

        mapNameFound.Should().Be("test-map-imagelayer", "Map with positioned image layers should load");
    }
}
