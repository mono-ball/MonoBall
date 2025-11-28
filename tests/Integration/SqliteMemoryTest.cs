using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PokeSharp.Game.Data;
using PokeSharp.Game.Data.Entities;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Integration.Tests;

/// <summary>
/// Integration test that validates SQLite memory usage in a realistic game startup scenario.
/// This test simulates the actual game initialization process.
/// </summary>
public class SqliteMemoryTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDbPath;
    private const long MaxMemoryBytesExpected = 100 * 1024 * 1024; // 100MB
    private readonly List<string> _loadedMaps = new();

    public SqliteMemoryTest(ITestOutputHelper output)
    {
        _output = output;
        _testDbPath = Path.Combine(
            Path.GetTempPath(),
            $"pokesharp_integration_{Guid.NewGuid()}.db"
        );
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task GameStartup_WithSqlite_StaysUnder100MB()
    {
        // Arrange - Simulate game initialization
        _output.WriteLine("=== SQLite Memory Integration Test ===");
        _output.WriteLine($"Database: {_testDbPath}");
        _output.WriteLine($"Max Expected Memory: {MaxMemoryBytesExpected / (1024 * 1024)}MB");
        _output.WriteLine("");

        var stopwatch = Stopwatch.StartNew();

        // Step 1: Force garbage collection to get baseline
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

        var baselineMemory = GC.GetTotalMemory(forceFullCollection: false);
        _output.WriteLine($"[BASELINE] Memory: {FormatBytes(baselineMemory)}");
        _output.WriteLine("");

        // Step 2: Create and initialize database (first run simulation)
        _output.WriteLine("[PHASE 1] Creating database and loading initial data...");
        await SimulateFirstGameStartup();

        var afterDbCreation = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"[PHASE 1] Memory after DB creation: {FormatBytes(afterDbCreation)}");
        _output.WriteLine(
            $"[PHASE 1] Memory increase: {FormatBytes(afterDbCreation - baselineMemory)}"
        );
        _output.WriteLine("");

        // Step 3: Simulate subsequent game startup (loading from existing DB)
        _output.WriteLine("[PHASE 2] Simulating subsequent game startup...");
        await SimulateSubsequentGameStartup();

        var afterSubsequentStartup = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine(
            $"[PHASE 2] Memory after subsequent startup: {FormatBytes(afterSubsequentStartup)}"
        );
        _output.WriteLine(
            $"[PHASE 2] Memory increase from baseline: {FormatBytes(afterSubsequentStartup - baselineMemory)}"
        );
        _output.WriteLine("");

        // Step 4: Simulate active gameplay queries
        _output.WriteLine("[PHASE 3] Simulating active gameplay queries...");
        await SimulateActiveGameplay();

        var afterGameplay = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine(
            $"[PHASE 3] Memory after gameplay simulation: {FormatBytes(afterGameplay)}"
        );
        _output.WriteLine(
            $"[PHASE 3] Memory increase from baseline: {FormatBytes(afterGameplay - baselineMemory)}"
        );
        _output.WriteLine("");

        stopwatch.Stop();

        // Step 5: Final memory check
        var finalMemoryUsage = afterGameplay - baselineMemory;

        _output.WriteLine("=== FINAL RESULTS ===");
        _output.WriteLine($"Total execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Final memory usage: {FormatBytes(finalMemoryUsage)}");
        _output.WriteLine($"Memory limit: {FormatBytes(MaxMemoryBytesExpected)}");
        _output.WriteLine($"Maps loaded: {_loadedMaps.Count}");
        _output.WriteLine(
            $"Status: {(finalMemoryUsage < MaxMemoryBytesExpected ? "✓ PASS" : "✗ FAIL")}"
        );
        _output.WriteLine("");

        // Log all loaded maps
        _output.WriteLine("=== LOADED MAPS ===");
        foreach (var map in _loadedMaps)
        {
            _output.WriteLine($"  - {map}");
        }

        // Assert
        Assert.True(
            finalMemoryUsage < MaxMemoryBytesExpected,
            $"Memory usage ({FormatBytes(finalMemoryUsage)}) exceeded limit ({FormatBytes(MaxMemoryBytesExpected)})"
        );
    }

    /// <summary>
    /// Simulates first game startup: database creation and initial data loading.
    /// </summary>
    private async Task SimulateFirstGameStartup()
    {
        var options = CreateSqliteOptions();

        await using var context = new GameDataContext(options);

        // Create database schema
        await context.Database.EnsureCreatedAsync();
        _output.WriteLine("  ✓ Database schema created");

        // Load realistic amount of game data
        await LoadGameData(context);

        _output.WriteLine($"  ✓ Loaded {await context.Maps.CountAsync()} maps");
        _output.WriteLine($"  ✓ Loaded {await context.Npcs.CountAsync()} NPCs");
        _output.WriteLine($"  ✓ Loaded {await context.Trainers.CountAsync()} trainers");
    }

    /// <summary>
    /// Simulates subsequent game startup: loading from existing database.
    /// </summary>
    private async Task SimulateSubsequentGameStartup()
    {
        var options = CreateSqliteOptions();

        await using var context = new GameDataContext(options);

        // Query summary data (what would happen on game load)
        var mapCount = await context.Maps.CountAsync();
        var npcCount = await context.Npcs.CountAsync();
        var trainerCount = await context.Trainers.CountAsync();

        _output.WriteLine($"  ✓ Database connection established");
        _output.WriteLine($"  ✓ Found {mapCount} maps");
        _output.WriteLine($"  ✓ Found {npcCount} NPCs");
        _output.WriteLine($"  ✓ Found {trainerCount} trainers");

        // Load initial map (e.g., player's starting location)
        var startingMap = await context.Maps.FirstOrDefaultAsync();
        if (startingMap != null)
        {
            _loadedMaps.Add($"{startingMap.MapId} - {startingMap.DisplayName}");
            _output.WriteLine($"  ✓ Loaded starting map: {startingMap.DisplayName}");
        }
    }

    /// <summary>
    /// Simulates active gameplay: loading maps, querying NPCs, etc.
    /// </summary>
    private async Task SimulateActiveGameplay()
    {
        var options = CreateSqliteOptions();

        // Simulate loading 10 different maps during gameplay
        for (int i = 0; i < 10; i++)
        {
            await using var context = new GameDataContext(options);

            // Load map data (as would happen when player moves to new area)
            var maps = await context.Maps.OrderBy(m => m.MapId).Skip(i * 5).Take(5).ToListAsync();

            foreach (var map in maps)
            {
                if (!_loadedMaps.Contains($"{map.MapId} - {map.DisplayName}"))
                {
                    _loadedMaps.Add($"{map.MapId} - {map.DisplayName}");
                }
            }

            // Load NPCs for current map (simulated)
            var npcs = await context.Npcs.Take(10).ToListAsync();

            _output.WriteLine(
                $"  ✓ Map transition {i + 1}: Loaded {maps.Count} maps, {npcs.Count} NPCs"
            );
        }

        // Simulate trainer battles (loading trainer data)
        await using (var context = new GameDataContext(options))
        {
            var trainers = await context.Trainers.Take(5).ToListAsync();
            _output.WriteLine($"  ✓ Loaded {trainers.Count} trainers for battles");
        }

        // Simulate region-based queries (e.g., Pokedex, map list)
        await using (var context = new GameDataContext(options))
        {
            var hoennMaps = await context
                .Maps.Where(m => m.Region == "hoenn")
                .Select(m => new { m.MapId, m.DisplayName })
                .ToListAsync();

            _output.WriteLine($"  ✓ Queried {hoennMaps.Count} Hoenn region maps");
        }
    }

    /// <summary>
    /// Loads realistic game data into the database.
    /// </summary>
    private async Task LoadGameData(GameDataContext context)
    {
        // Load maps (simulating ~500 game maps)
        var maps = new List<MapDefinition>();
        for (int i = 0; i < 500; i++)
        {
            maps.Add(
                new MapDefinition
                {
                    MapId = $"map_{i:D3}",
                    DisplayName = GetMapDisplayName(i),
                    Region = i < 400 ? "hoenn" : "kanto",
                    MapType = GetMapType(i),
                    TiledDataJson = GenerateRealisticTiledJson(i),
                    MusicId = $"bgm_{i % 20}",
                    Weather = GetWeather(i),
                    ShowMapName = true,
                    CanFly = i % 10 == 0,
                }
            );
        }
        context.Maps.AddRange(maps);

        // Load NPCs (simulating ~1000 NPCs)
        var npcs = new List<NpcDefinition>();
        for (int i = 0; i < 1000; i++)
        {
            npcs.Add(
                new NpcDefinition
                {
                    NpcId = $"npc_{i:D4}",
                    DisplayName = $"NPC {i}",
                    NpcType = GetNpcType(i),
                }
            );
        }
        context.Npcs.AddRange(npcs);

        // Load trainers (simulating ~200 trainers)
        var trainers = new List<TrainerDefinition>();
        for (int i = 0; i < 200; i++)
        {
            trainers.Add(
                new TrainerDefinition
                {
                    TrainerId = $"trainer_{i:D3}",
                    DisplayName = $"Trainer {i}",
                    TrainerClass = GetTrainerClass(i),
                }
            );
        }
        context.Trainers.AddRange(trainers);

        await context.SaveChangesAsync();
    }

    #region Helper Methods

    private DbContextOptions<GameDataContext> CreateSqliteOptions()
    {
        return new DbContextOptionsBuilder<GameDataContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .EnableSensitiveDataLogging(false) // Disable for performance
            .EnableDetailedErrors(false)
            .Options;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string GetMapDisplayName(int index)
    {
        var names = new[]
        {
            "Littleroot Town",
            "Route 101",
            "Oldale Town",
            "Route 102",
            "Petalburg City",
            "Petalburg Woods",
            "Route 104",
            "Rustboro City",
            "Dewford Town",
            "Slateport City",
        };
        return names[index % names.Length] + $" #{index / names.Length}";
    }

    private string GetMapType(int index)
    {
        var types = new[] { "town", "route", "city", "cave", "building", "forest" };
        return types[index % types.Length];
    }

    private string GetWeather(int index)
    {
        var weathers = new[] { "clear", "rain", "sandstorm", "snow", "fog" };
        return weathers[index % weathers.Length];
    }

    private string GetNpcType(int index)
    {
        var types = new[] { "generic", "nurse", "shopkeeper", "trainer", "rival", "professor" };
        return types[index % types.Length];
    }

    private string GetTrainerClass(int index)
    {
        var classes = new[]
        {
            "youngster",
            "lass",
            "bug_catcher",
            "fisherman",
            "swimmer",
            "ace_trainer",
        };
        return classes[index % classes.Length];
    }

    private string GenerateRealisticTiledJson(int seed)
    {
        // Generate realistic-sized Tiled JSON (~5-10KB per map)
        var width = 20 + (seed % 30);
        var height = 15 + (seed % 20);
        var tileData = new int[width * height];

        for (int i = 0; i < tileData.Length; i++)
        {
            tileData[i] = (seed + i) % 1000;
        }

        var data = new
        {
            width,
            height,
            tilewidth = 16,
            tileheight = 16,
            infinite = false,
            layers = new[]
            {
                new
                {
                    name = "Ground",
                    type = "tilelayer",
                    data = tileData.Take(tileData.Length / 3).ToArray(),
                },
                new
                {
                    name = "Objects",
                    type = "tilelayer",
                    data = tileData.Skip(tileData.Length / 3).Take(tileData.Length / 3).ToArray(),
                },
                new
                {
                    name = "Collision",
                    type = "tilelayer",
                    data = tileData.Skip(2 * tileData.Length / 3).ToArray(),
                },
            },
            tilesets = new[] { new { firstgid = 1, source = "tileset.tsx" } },
        };

        return System.Text.Json.JsonSerializer.Serialize(data);
    }

    #endregion
}
