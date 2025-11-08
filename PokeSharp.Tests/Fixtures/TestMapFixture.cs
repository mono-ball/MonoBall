using PokeSharp.Rendering.Loaders.Tmx;

namespace PokeSharp.Tests.Fixtures;

/// <summary>
/// Provides reusable test map fixtures for unit tests
/// </summary>
public static class TestMapFixture
{
    /// <summary>
    /// Creates a simple 3x3 test map with basic configuration
    /// </summary>
    public static TmxDocument CreateSimpleMap()
    {
        var data = new int[3, 3]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        };

        return new TmxDocument
        {
            Width = 3,
            Height = 3,
            TileWidth = 16,
            TileHeight = 16,
            Layers = new List<TmxLayer>
            {
                new()
                {
                    Name = "Ground",
                    Width = 3,
                    Height = 3,
                    Data = data
                }
            }
        };
    }

    /// <summary>
    /// Creates a multi-layer test map
    /// </summary>
    public static TmxDocument CreateMultiLayerMap()
    {
        var groundData = new int[5, 5];
        var counter = 1;
        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 5; x++)
            groundData[y, x] = counter++;

        return new TmxDocument
        {
            Width = 5,
            Height = 5,
            TileWidth = 16,
            TileHeight = 16,
            Layers = new List<TmxLayer>
            {
                new()
                {
                    Name = "Ground",
                    Width = 5,
                    Height = 5,
                    Data = groundData
                },
                new()
                {
                    Name = "Objects",
                    Width = 5,
                    Height = 5,
                    Data = new int[5, 5] // Sparse layer
                },
                new()
                {
                    Name = "Collision",
                    Width = 5,
                    Height = 5,
                    Data = new int[5, 5]
                }
            }
        };
    }

    /// <summary>
    /// Creates a map with flipped tiles
    /// </summary>
    public static TmxDocument CreateMapWithFlips()
    {
        const int FLIP_HORIZONTAL = unchecked((int)0x80000000);
        const int FLIP_VERTICAL = 0x40000000;
        const int FLIP_DIAGONAL = 0x20000000;

        var data = new int[2, 2]
        {
            { 1, FLIP_HORIZONTAL | 2 },           // Normal, Horizontal flip
            { FLIP_VERTICAL | 3, FLIP_DIAGONAL | 4 }  // Vertical flip, Diagonal flip
        };

        return new TmxDocument
        {
            Width = 2,
            Height = 2,
            TileWidth = 16,
            TileHeight = 16,
            Layers = new List<TmxLayer>
            {
                new()
                {
                    Name = "Ground",
                    Width = 2,
                    Height = 2,
                    Data = data
                }
            }
        };
    }

    /// <summary>
    /// Gets the path to the test map JSON file
    /// </summary>
    public static string GetTestMapPath()
    {
        return Path.GetFullPath(Path.Combine("TestData", "test-map.json"));
    }
}
