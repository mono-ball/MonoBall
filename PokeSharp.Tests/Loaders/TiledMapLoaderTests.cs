using PokeSharp.Rendering.Loaders;
using Xunit;

namespace PokeSharp.Tests.Loaders;

/// <summary>
///     Unit tests for TiledMapLoader compression support.
/// </summary>
public class TiledMapLoaderTests
{
    [Fact]
    public void Load_UncompressedMap_LoadsSuccessfully()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map.json";

        // Act
        var tmxDoc = TiledMapLoader.Load(mapPath);

        // Assert
        Assert.NotNull(tmxDoc);
        Assert.Equal(3, tmxDoc.Width);
        Assert.Equal(3, tmxDoc.Height);
        Assert.Single(tmxDoc.Layers);

        var layer = tmxDoc.Layers[0];
        Assert.Equal("Ground", layer.Name);
        Assert.NotNull(layer.Data);
        Assert.Equal(3, layer.Data!.GetLength(0)); // Height
        Assert.Equal(3, layer.Data!.GetLength(1)); // Width

        // Verify tile data
        Assert.Equal(1, layer.Data[0, 0]);
        Assert.Equal(2, layer.Data[0, 1]);
        Assert.Equal(3, layer.Data[0, 2]);
        Assert.Equal(4, layer.Data[1, 0]);
        Assert.Equal(5, layer.Data[1, 1]);
        Assert.Equal(6, layer.Data[1, 2]);
        Assert.Equal(7, layer.Data[2, 0]);
        Assert.Equal(8, layer.Data[2, 1]);
        Assert.Equal(9, layer.Data[2, 2]);
    }

    [Fact]
    public void Load_ZstdCompressedMap_LoadsSuccessfully()
    {
        // Arrange
        var mapPath = "PokeSharp.Tests/TestData/test-map-zstd-3x3.json";

        // Act
        var tmxDoc = TiledMapLoader.Load(mapPath);

        // Assert
        Assert.NotNull(tmxDoc);
        Assert.Equal(3, tmxDoc.Width);
        Assert.Equal(3, tmxDoc.Height);
        Assert.Single(tmxDoc.Layers);

        var layer = tmxDoc.Layers[0];
        Assert.Equal("Ground", layer.Name);
        Assert.NotNull(layer.Data);
        Assert.Equal(3, layer.Data!.GetLength(0)); // Height
        Assert.Equal(3, layer.Data!.GetLength(1)); // Width

        // Verify decompressed tile data matches expected values
        Assert.Equal(1, layer.Data[0, 0]);
        Assert.Equal(2, layer.Data[0, 1]);
        Assert.Equal(3, layer.Data[0, 2]);
        Assert.Equal(4, layer.Data[1, 0]);
        Assert.Equal(5, layer.Data[1, 1]);
        Assert.Equal(6, layer.Data[1, 2]);
        Assert.Equal(7, layer.Data[2, 0]);
        Assert.Equal(8, layer.Data[2, 1]);
        Assert.Equal(9, layer.Data[2, 2]);
    }

    [Fact]
    public void Load_ZstdCompressedMap_ProducesSameResultAsUncompressed()
    {
        // Arrange
        var uncompressedPath = "PokeSharp.Tests/TestData/test-map.json";
        var compressedPath = "PokeSharp.Tests/TestData/test-map-zstd-3x3.json";

        // Act
        var uncompressedDoc = TiledMapLoader.Load(uncompressedPath);
        var compressedDoc = TiledMapLoader.Load(compressedPath);

        // Assert - Both should produce identical tile data
        Assert.Equal(uncompressedDoc.Width, compressedDoc.Width);
        Assert.Equal(uncompressedDoc.Height, compressedDoc.Height);
        Assert.Equal(uncompressedDoc.Layers.Count, compressedDoc.Layers.Count);

        for (var layerIdx = 0; layerIdx < uncompressedDoc.Layers.Count; layerIdx++)
        {
            var uncompressedLayer = uncompressedDoc.Layers[layerIdx];
            var compressedLayer = compressedDoc.Layers[layerIdx];

            Assert.Equal(uncompressedLayer.Name, compressedLayer.Name);
            Assert.NotNull(uncompressedLayer.Data);
            Assert.NotNull(compressedLayer.Data);

            var height = uncompressedLayer.Data!.GetLength(0);
            var width = uncompressedLayer.Data!.GetLength(1);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Assert.Equal(
                        uncompressedLayer.Data[y, x],
                        compressedLayer.Data![y, x]
                    );
                }
            }
        }
    }
}
