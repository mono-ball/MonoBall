using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Modding;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.Content.Benchmarks;

/// <summary>
///     Performance benchmarks for ContentProvider operations.
///     These tests measure and validate performance targets for critical operations.
/// </summary>
public class ContentProviderBenchmarks : IDisposable
{
    // Performance targets (in milliseconds)
    private const double CacheHitTarget = 0.1;
    private const double CacheMissTarget = 10.0;
    private const double BulkRetrievalTarget = 100.0;
    private const double LruCacheOperationTarget = 0.01;

    // Benchmark iteration counts
    private const int WarmupIterations = 100;
    private const int BenchmarkIterations = 1000;
    private const int BulkFileCount = 1000;
    private readonly string _baseGameRoot;
    private readonly string _modsRoot;
    private readonly ITestOutputHelper _output;
    private readonly string _testRoot;

    public ContentProviderBenchmarks(ITestOutputHelper output)
    {
        _output = output;

        // Create temporary test directory structure
        _testRoot = Path.Combine(Path.GetTempPath(), "ContentProviderBenchmarks_" + Guid.NewGuid().ToString("N"));
        _baseGameRoot = Path.Combine(_testRoot, "Assets");
        _modsRoot = Path.Combine(_testRoot, "Mods");

        Directory.CreateDirectory(_baseGameRoot);
        Directory.CreateDirectory(_modsRoot);

        // Create base game content structure
        Directory.CreateDirectory(Path.Combine(_baseGameRoot, "Definitions"));
        Directory.CreateDirectory(Path.Combine(_baseGameRoot, "Graphics"));
    }

    public void Dispose()
    {
        // Cleanup test directories
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private ContentProvider CreateContentProvider(IModLoader? modLoader = null)
    {
        modLoader ??= CreateMockModLoader();

        IOptions<ContentProviderOptions> options = Options.Create(new ContentProviderOptions
        {
            BaseGameRoot = _baseGameRoot,
            MaxCacheSize = 10000,
            LogCacheMisses = false,
            ThrowOnPathTraversal = true,
            BaseContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        });

        NullLogger<ContentProvider> logger = NullLogger<ContentProvider>.Instance;

        return new ContentProvider(modLoader, logger, options);
    }

    private IModLoader CreateMockModLoader(params ModManifest[] manifests)
    {
        var mockModLoader = new Mock<IModLoader>();
        var loadedMods = manifests.ToDictionary(m => m.Id, m => m);

        mockModLoader.Setup(m => m.LoadedMods)
            .Returns(loadedMods);

        return mockModLoader.Object;
    }

    private void CreateTestFile(string relativePath, string content = "test")
    {
        string fullPath = Path.Combine(_testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private IModLoader CreateMockModLoaderWithMultipleMods(int modCount)
    {
        var manifests = new List<ModManifest>();

        for (int i = 0; i < modCount; i++)
        {
            string modId = $"test-mod-{i}";
            string modPath = Path.Combine(_modsRoot, modId);
            Directory.CreateDirectory(modPath);
            Directory.CreateDirectory(Path.Combine(modPath, "Definitions"));

            manifests.Add(new ModManifest
            {
                Id = modId,
                Name = $"Test Mod {i}",
                Version = "1.0.0",
                Priority = i * 10,
                DirectoryPath = modPath,
                ContentFolders = new Dictionary<string, string> { ["Definitions"] = "Definitions" }
            });
        }

        return CreateMockModLoader(manifests.ToArray());
    }

    /// <summary>
    ///     Measures the average time for a single operation over multiple iterations.
    /// </summary>
    private double MeasureAverageTime(Action operation, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            operation();
        }

        stopwatch.Stop();

        return (double)stopwatch.ElapsedMilliseconds / iterations;
    }

    [Fact]
    public void ResolveContentPath_CacheHit_MeetsPerformanceTarget()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/benchmark.json");
        ContentProvider provider = CreateContentProvider();

        // Warm up cache
        for (int i = 0; i < WarmupIterations; i++)
        {
            provider.ResolveContentPath("Definitions", "benchmark.json");
        }

        // Act - Measure cache hit performance
        double avgTime = MeasureAverageTime(
            () => provider.ResolveContentPath("Definitions", "benchmark.json"),
            BenchmarkIterations);

        ContentProviderStats stats = provider.GetStats();

        // Assert
        _output.WriteLine($"ResolveContentPath (Cache Hit): {avgTime:F6} ms avg over {BenchmarkIterations} iterations");
        _output.WriteLine(
            $"Cache Stats - Hits: {stats.CacheHits}, Misses: {stats.CacheMisses}, Hit Rate: {stats.HitRate:P2}");

        avgTime.Should().BeLessThan(CacheHitTarget,
            $"Cache hit should complete in less than {CacheHitTarget}ms on average");

        // Verify cache is being used
        stats.HitRate.Should().BeGreaterThan(0.95, "Should have >95% cache hit rate");
    }

    [Fact]
    public void ResolveContentPath_CacheMiss_MeetsPerformanceTarget()
    {
        // Arrange
        IModLoader modLoader = CreateMockModLoaderWithMultipleMods(5);
        ContentProvider provider = CreateContentProvider(modLoader);

        // Create test files in multiple mods
        for (int i = 0; i < 5; i++)
        {
            CreateTestFile($"Mods/test-mod-{i}/Definitions/file{i}.json");
        }

        // Create unique file names to force cache misses
        var testFiles = Enumerable.Range(0, BenchmarkIterations)
            .Select(i => $"file{i % 5}.json")
            .ToList();

        // Warm up
        foreach (string file in testFiles.Take(WarmupIterations))
        {
            provider.ResolveContentPath("Definitions", file);
        }

        // Clear cache to ensure misses
        provider.InvalidateCache();

        // Act - Measure cache miss performance
        var stopwatch = Stopwatch.StartNew();

        foreach (string file in testFiles)
        {
            provider.ResolveContentPath("Definitions", file);
        }

        stopwatch.Stop();
        double avgTime = (double)stopwatch.ElapsedMilliseconds / testFiles.Count;

        ContentProviderStats stats = provider.GetStats();

        // Assert
        _output.WriteLine($"ResolveContentPath (Cache Miss): {avgTime:F6} ms avg over {testFiles.Count} iterations");
        _output.WriteLine($"Cache Stats - Hits: {stats.CacheHits}, Misses: {stats.CacheMisses}");

        avgTime.Should().BeLessThan(CacheMissTarget,
            $"Cache miss should complete in less than {CacheMissTarget}ms on average");
    }

    [Fact]
    public void GetAllContentPaths_1000Files_MeetsPerformanceTarget()
    {
        // Arrange
        IModLoader modLoader = CreateMockModLoaderWithMultipleMods(3);
        ContentProvider provider = CreateContentProvider(modLoader);

        // Create 1000 test files across mods and base game
        int filesPerLocation = BulkFileCount / 4;

        // Base game files
        for (int i = 0; i < filesPerLocation; i++)
        {
            CreateTestFile($"Assets/Definitions/base{i}.json");
        }

        // Files in each mod
        for (int modIdx = 0; modIdx < 3; modIdx++)
        {
            for (int i = 0; i < filesPerLocation; i++)
            {
                CreateTestFile($"Mods/test-mod-{modIdx}/Definitions/mod{modIdx}_file{i}.json");
            }
        }

        // Warm up
        for (int i = 0; i < 3; i++)
        {
            _ = provider.GetAllContentPaths("Definitions").ToList();
        }

        // Act - Measure bulk retrieval performance
        var stopwatch = Stopwatch.StartNew();
        var paths = provider.GetAllContentPaths("Definitions").ToList();
        stopwatch.Stop();

        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"GetAllContentPaths: {elapsedMs:F2} ms for {paths.Count} files");
        _output.WriteLine($"Average per file: {elapsedMs / paths.Count:F6} ms");

        elapsedMs.Should().BeLessThan(BulkRetrievalTarget,
            $"Bulk retrieval should complete in less than {BulkRetrievalTarget}ms");

        paths.Count.Should().BeGreaterOrEqualTo((int)(BulkFileCount * 0.95),
            "Should retrieve approximately the expected number of files");
    }

    [Fact]
    public void LruCache_Set_Get_Performance_MeetsTarget()
    {
        // Arrange
        var cache = new LruCache<string, string>(10000);
        var testData = Enumerable.Range(0, BenchmarkIterations)
            .Select(i => (Key: $"key{i}", Value: $"value{i}"))
            .ToList();

        // Warm up
        for (int i = 0; i < WarmupIterations; i++)
        {
            cache.Set(testData[i].Key, testData[i].Value);
            cache.TryGet(testData[i].Key, out _);
        }

        // Act - Measure Set performance
        var setStopwatch = Stopwatch.StartNew();
        foreach ((string? key, string? value) in testData)
        {
            cache.Set(key, value);
        }

        setStopwatch.Stop();

        double avgSetTime = (double)setStopwatch.ElapsedMilliseconds / testData.Count;

        // Act - Measure Get performance
        var getStopwatch = Stopwatch.StartNew();
        foreach ((string? key, string _) in testData)
        {
            cache.TryGet(key, out _);
        }

        getStopwatch.Stop();

        double avgGetTime = (double)getStopwatch.ElapsedMilliseconds / testData.Count;

        // Assert
        _output.WriteLine($"LRU Cache Set: {avgSetTime:F6} ms avg over {testData.Count} operations");
        _output.WriteLine($"LRU Cache Get: {avgGetTime:F6} ms avg over {testData.Count} operations");
        _output.WriteLine($"Total Cache Size: {cache.Count} entries");

        avgSetTime.Should().BeLessThan(LruCacheOperationTarget,
            $"Cache Set should complete in less than {LruCacheOperationTarget}ms on average");

        avgGetTime.Should().BeLessThan(LruCacheOperationTarget,
            $"Cache Get should complete in less than {LruCacheOperationTarget}ms on average");
    }

    [Fact]
    public void LruCache_Eviction_Performance()
    {
        // Arrange
        var cache = new LruCache<int, string>(1000);

        // Fill cache to capacity
        for (int i = 0; i < 1000; i++)
        {
            cache.Set(i, $"value{i}");
        }

        // Act - Measure eviction performance when cache is full
        var stopwatch = Stopwatch.StartNew();

        for (int i = 1000; i < 1000 + BenchmarkIterations; i++)
        {
            cache.Set(i, $"value{i}");
        }

        stopwatch.Stop();

        double avgTime = (double)stopwatch.ElapsedMilliseconds / BenchmarkIterations;

        // Assert
        _output.WriteLine($"LRU Cache Eviction: {avgTime:F6} ms avg over {BenchmarkIterations} operations");
        _output.WriteLine($"Final Cache Size: {cache.Count} entries");

        cache.Count.Should().Be(1000, "Cache should maintain maximum size");

        avgTime.Should().BeLessThan(LruCacheOperationTarget * 2,
            "Cache eviction should not significantly degrade performance");
    }

    [Fact]
    public void ContentProvider_Mixed_Operations_Performance()
    {
        // Arrange
        IModLoader modLoader = CreateMockModLoaderWithMultipleMods(3);
        ContentProvider provider = CreateContentProvider(modLoader);

        // Create test files
        for (int i = 0; i < 100; i++)
        {
            CreateTestFile($"Assets/Definitions/mixed{i}.json");
        }

        // Mix of operations
        var operations = new Action[]
        {
            () => provider.ResolveContentPath("Definitions", "mixed1.json"),
            () => provider.ContentExists("Definitions", "mixed2.json"),
            () => provider.GetContentSource("Definitions", "mixed3.json"),
            () => provider.ResolveContentPath("Definitions", "mixed4.json"),
            () => provider.ContentExists("Definitions", "mixed5.json")
        };

        // Warm up
        for (int i = 0; i < WarmupIterations; i++)
        {
            foreach (Action op in operations)
            {
                op();
            }
        }

        // Act - Measure mixed operation performance
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < BenchmarkIterations; i++)
        {
            foreach (Action op in operations)
            {
                op();
            }
        }

        stopwatch.Stop();

        double avgTimePerOperation = (double)stopwatch.ElapsedMilliseconds / (BenchmarkIterations * operations.Length);
        ContentProviderStats stats = provider.GetStats();

        // Assert
        _output.WriteLine($"Mixed Operations: {avgTimePerOperation:F6} ms avg per operation");
        _output.WriteLine($"Total Operations: {BenchmarkIterations * operations.Length}");
        _output.WriteLine(
            $"Cache Stats - Hits: {stats.CacheHits}, Misses: {stats.CacheMisses}, Hit Rate: {stats.HitRate:P2}");

        avgTimePerOperation.Should().BeLessThan(CacheHitTarget * 2,
            "Mixed operations should maintain good performance");

        stats.HitRate.Should().BeGreaterThan(0.9,
            "Cache should be effective for mixed operations");
    }

    [Fact]
    public void ContentProvider_Concurrent_Access_Performance()
    {
        // Arrange
        IModLoader modLoader = CreateMockModLoaderWithMultipleMods(3);
        ContentProvider provider = CreateContentProvider(modLoader);

        // Create test files
        for (int i = 0; i < 50; i++)
        {
            CreateTestFile($"Assets/Definitions/concurrent{i}.json");
        }

        int threadCount = Environment.ProcessorCount;
        int iterationsPerThread = 200;

        // Act - Measure concurrent access performance
        var stopwatch = Stopwatch.StartNew();

        Task[] tasks = Enumerable.Range(0, threadCount)
            .Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    int fileIdx = ((threadId * iterationsPerThread) + i) % 50;
                    provider.ResolveContentPath("Definitions", $"concurrent{fileIdx}.json");
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);
        stopwatch.Stop();

        int totalOperations = threadCount * iterationsPerThread;
        double avgTime = (double)stopwatch.ElapsedMilliseconds / totalOperations;
        ContentProviderStats stats = provider.GetStats();

        // Assert
        _output.WriteLine($"Concurrent Access: {avgTime:F6} ms avg per operation");
        _output.WriteLine($"Thread Count: {threadCount}, Total Operations: {totalOperations}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine(
            $"Cache Stats - Hits: {stats.CacheHits}, Misses: {stats.CacheMisses}, Hit Rate: {stats.HitRate:P2}");

        avgTime.Should().BeLessThan(CacheHitTarget * 5,
            "Concurrent access should maintain reasonable performance");

        stats.TotalResolutions.Should().Be(totalOperations,
            "All operations should be tracked");
    }
}
