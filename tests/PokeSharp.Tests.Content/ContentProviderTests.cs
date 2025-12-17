using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Modding;
using Moq;
using Xunit;

namespace PokeSharp.Tests.Content;

/// <summary>
///     Unit tests for the ContentProvider class.
/// </summary>
public class ContentProviderTests : IDisposable
{
    private readonly string _baseGameRoot;
    private readonly string _modsRoot;
    private readonly string _testRoot;

    public ContentProviderTests()
    {
        // Create temporary test directory structure
        _testRoot = Path.Combine(Path.GetTempPath(), "ContentProviderTests_" + Guid.NewGuid().ToString("N"));
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
            Directory.Delete(_testRoot, true);
        }
    }

    private ContentProvider CreateContentProvider(IModLoader? modLoader = null)
    {
        modLoader ??= CreateMockModLoader();

        IOptions<ContentProviderOptions> options = Options.Create(new ContentProviderOptions
        {
            BaseGameRoot = _baseGameRoot,
            MaxCacheSize = 1000,
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

    [Fact]
    public void ResolveContentPath_BaseGameContent_ReturnsBasePath()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/test.json");
        ContentProvider provider = CreateContentProvider();

        // Act
        string? result = provider.ResolveContentPath("Definitions", "test.json");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Assets");
        result.Should().EndWith("test.json");
    }

    [Fact]
    public void ResolveContentPath_RootContentType_ResolvesRootLevelAssets()
    {
        // Arrange - Create root-level file (like logo.png or MonoBall.wav)
        CreateTestFile("Assets/logo.png", "test logo");

        IOptions<ContentProviderOptions> options = Options.Create(new ContentProviderOptions
        {
            BaseGameRoot = _baseGameRoot,
            MaxCacheSize = 1000,
            LogCacheMisses = false,
            ThrowOnPathTraversal = true,
            BaseContentFolders = new Dictionary<string, string>
            {
                ["Root"] = "", // Empty string for root-level assets
                ["Definitions"] = "Definitions",
                ["Graphics"] = "Graphics"
            }
        });

        var provider = new ContentProvider(CreateMockModLoader(), NullLogger<ContentProvider>.Instance, options);

        // Act
        string? result = provider.ResolveContentPath("Root", "logo.png");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Assets");
        result.Should().EndWith("logo.png");
        // Verify Path.Combine correctly handles empty folder: Assets + "" + logo.png = Assets/logo.png
        result.Should().NotContain("//"); // No double slashes from empty path segment
        File.Exists(result!).Should().BeTrue("resolved path should exist");
    }

    [Fact]
    public void ResolveContentPath_ContentNotFound_ReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        string? result = provider.ResolveContentPath("Definitions", "nonexistent.json");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveContentPath_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/cached.json");
        ContentProvider provider = CreateContentProvider();

        // Act - First call populates cache
        string? result1 = provider.ResolveContentPath("Definitions", "cached.json");
        ContentProviderStats stats1 = provider.GetStats();

        // Second call should hit cache
        string? result2 = provider.ResolveContentPath("Definitions", "cached.json");
        ContentProviderStats stats2 = provider.GetStats();

        // Assert
        result1.Should().Be(result2);
        stats1.CacheMisses.Should().Be(1);
        stats2.CacheHits.Should().Be(1);
    }

    [Fact]
    public void ResolveContentPath_PathTraversal_ThrowsSecurityException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        Func<string?> act = () => provider.ResolveContentPath("Definitions", "../../../etc/passwd");

        // Assert
        act.Should().Throw<SecurityException>();
    }

    [Fact]
    public void ResolveContentPath_RootedPath_ThrowsSecurityException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        Func<string?> act = () => provider.ResolveContentPath("Definitions", "/etc/passwd");

        // Assert
        act.Should().Throw<SecurityException>();
    }

    [Fact]
    public void ResolveContentPath_NullContentType_ThrowsArgumentException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        Func<string?> act = () => provider.ResolveContentPath(null!, "test.json");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveContentPath_EmptyRelativePath_ThrowsArgumentException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        Func<string?> act = () => provider.ResolveContentPath("Definitions", "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContentExists_ExistingContent_ReturnsTrue()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/exists.json");
        ContentProvider provider = CreateContentProvider();

        // Act
        bool result = provider.ContentExists("Definitions", "exists.json");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContentExists_NonExistingContent_ReturnsFalse()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        bool result = provider.ContentExists("Definitions", "notexists.json");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetContentSource_BaseGameContent_ReturnsBase()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/base.json");
        ContentProvider provider = CreateContentProvider();

        // Act
        string? source = provider.GetContentSource("Definitions", "base.json");

        // Assert
        source.Should().Be("base");
    }

    [Fact]
    public void GetContentSource_NonExistingContent_ReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        string? source = provider.GetContentSource("Definitions", "notexists.json");

        // Assert
        source.Should().BeNull();
    }

    [Fact]
    public void InvalidateCache_ClearsAllEntries()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/file1.json");
        CreateTestFile("Assets/Definitions/file2.json");
        ContentProvider provider = CreateContentProvider();

        // Populate cache
        provider.ResolveContentPath("Definitions", "file1.json");
        provider.ResolveContentPath("Definitions", "file2.json");

        ContentProviderStats statsBefore = provider.GetStats();

        // Act
        provider.InvalidateCache();

        // Access again
        provider.ResolveContentPath("Definitions", "file1.json");
        ContentProviderStats statsAfter = provider.GetStats();

        // Assert
        statsBefore.CacheHits.Should().Be(0);
        statsAfter.CacheMisses.Should().BeGreaterThan(statsBefore.CacheMisses);
    }

    [Fact]
    public void InvalidateCache_SpecificType_OnlyClearsMatchingEntries()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/def.json");
        CreateTestFile("Assets/Graphics/gfx.json");
        ContentProvider provider = CreateContentProvider();

        // Populate cache
        provider.ResolveContentPath("Definitions", "def.json");
        provider.ResolveContentPath("Graphics", "gfx.json");

        // Act - Invalidate only Definitions
        provider.InvalidateCache("Definitions");

        // Access both again
        provider.ResolveContentPath("Definitions", "def.json"); // Should be cache miss
        provider.ResolveContentPath("Graphics", "gfx.json"); // Should be cache hit

        ContentProviderStats stats = provider.GetStats();

        // Assert - One hit (Graphics), one miss (Definitions after invalidation)
        stats.CacheHits.Should().Be(1);
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/stats.json");
        ContentProvider provider = CreateContentProvider();

        // Act
        provider.ResolveContentPath("Definitions", "stats.json"); // Miss
        provider.ResolveContentPath("Definitions", "stats.json"); // Hit
        provider.ResolveContentPath("Definitions", "stats.json"); // Hit
        provider.ResolveContentPath("Definitions", "notexists.json"); // Miss (negative cache)
        provider.ResolveContentPath("Definitions", "notexists.json"); // Hit (negative cache)

        ContentProviderStats stats = provider.GetStats();

        // Assert
        stats.TotalResolutions.Should().Be(5);
        stats.CacheHits.Should().Be(3);
        stats.CacheMisses.Should().Be(2);
        stats.HitRate.Should().BeApproximately(0.6, 0.01);
    }

    [Fact]
    public void GetAllContentPaths_ReturnsAllMatchingFiles()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/file1.json");
        CreateTestFile("Assets/Definitions/file2.json");
        CreateTestFile("Assets/Definitions/subdir/file3.json");
        CreateTestFile("Assets/Definitions/notjson.txt");
        ContentProvider provider = CreateContentProvider();

        // Act
        var paths = provider.GetAllContentPaths("Definitions").ToList();

        // Assert
        paths.Should().HaveCount(3);
        paths.Should().Contain(p => p.EndsWith("file1.json"));
        paths.Should().Contain(p => p.EndsWith("file2.json"));
        paths.Should().Contain(p => p.EndsWith("file3.json"));
        paths.Should().NotContain(p => p.EndsWith("notjson.txt"));
    }

    [Fact]
    public void GetAllContentPaths_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        var paths = provider.GetAllContentPaths("Definitions").ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void GetAllContentPaths_InvalidContentType_ReturnsEmpty()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act
        var paths = provider.GetAllContentPaths("InvalidType").ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void ResolveContentPath_ModOverridesBase_ReturnsModPath()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/pokemon.json", "base content");

        // Create mod directory and file
        string modPath = Path.Combine(_modsRoot, "TestMod");
        Directory.CreateDirectory(Path.Combine(modPath, "Definitions"));
        File.WriteAllText(Path.Combine(modPath, "Definitions", "pokemon.json"), "mod content");

        var modManifest = new ModManifest
        {
            Id = "test-mod",
            Name = "Test Mod",
            Version = "1.0.0",
            DirectoryPath = modPath,
            Priority = 100,
            ContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        };

        ContentProvider provider = CreateContentProvider(CreateMockModLoader(modManifest));

        // Act
        string? result = provider.ResolveContentPath("Definitions", "pokemon.json");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("TestMod");
        result.Should().EndWith("pokemon.json");
        File.ReadAllText(result!).Should().Be("mod content");
    }

    [Fact]
    public void ResolveContentPath_NoModOverride_ReturnsBasePath()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/baseonly.json", "base content");

        // Create mod directory without the file
        string modPath = Path.Combine(_modsRoot, "TestMod");
        Directory.CreateDirectory(Path.Combine(modPath, "Definitions"));

        var modManifest = new ModManifest
        {
            Id = "test-mod",
            Name = "Test Mod",
            Version = "1.0.0",
            DirectoryPath = modPath,
            Priority = 100,
            ContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        };

        ContentProvider provider = CreateContentProvider(CreateMockModLoader(modManifest));

        // Act
        string? result = provider.ResolveContentPath("Definitions", "baseonly.json");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Assets");
        result.Should().NotContain("TestMod");
        result.Should().EndWith("baseonly.json");
        File.ReadAllText(result!).Should().Be("base content");
    }

    [Fact]
    public void GetContentSource_ModContent_ReturnsModId()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/base.json", "base content");

        // Create mod directory and file
        string modPath = Path.Combine(_modsRoot, "AwesomeMod");
        Directory.CreateDirectory(Path.Combine(modPath, "Definitions"));
        File.WriteAllText(Path.Combine(modPath, "Definitions", "modfile.json"), "mod content");

        var modManifest = new ModManifest
        {
            Id = "awesome-mod",
            Name = "Awesome Mod",
            Version = "2.0.0",
            DirectoryPath = modPath,
            Priority = 50,
            ContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        };

        ContentProvider provider = CreateContentProvider(CreateMockModLoader(modManifest));

        // Act
        string? source = provider.GetContentSource("Definitions", "modfile.json");

        // Assert
        source.Should().Be("awesome-mod");
    }

    [Fact]
    public void GetAllContentPaths_ModAndBase_NoDuplicates()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/shared.json", "base content");
        CreateTestFile("Assets/Definitions/baseonly.json", "base only");

        // Create mod directory with overlapping and unique files
        string modPath = Path.Combine(_modsRoot, "TestMod");
        Directory.CreateDirectory(Path.Combine(modPath, "Definitions"));
        File.WriteAllText(Path.Combine(modPath, "Definitions", "shared.json"), "mod override");
        File.WriteAllText(Path.Combine(modPath, "Definitions", "modonly.json"), "mod only");

        var modManifest = new ModManifest
        {
            Id = "test-mod",
            Name = "Test Mod",
            Version = "1.0.0",
            DirectoryPath = modPath,
            Priority = 100,
            ContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        };

        ContentProvider provider = CreateContentProvider(CreateMockModLoader(modManifest));

        // Act
        var paths = provider.GetAllContentPaths("Definitions").ToList();

        // Assert
        paths.Should().HaveCount(3); // shared.json (mod version only), baseonly.json, modonly.json
        paths.Should().Contain(p => p.EndsWith("shared.json"));
        paths.Should().Contain(p => p.EndsWith("baseonly.json"));
        paths.Should().Contain(p => p.EndsWith("modonly.json"));

        // Verify the shared.json path points to mod version
        string sharedPath = paths.First(p => p.EndsWith("shared.json"));
        File.ReadAllText(sharedPath).Should().Be("mod override");
    }

    [Fact]
    public void ResolveContentPath_MultipleModsSamePriority_FirstLoaded()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/conflict.json", "base content");

        // Create first mod
        string mod1Path = Path.Combine(_modsRoot, "Mod1");
        Directory.CreateDirectory(Path.Combine(mod1Path, "Definitions"));
        File.WriteAllText(Path.Combine(mod1Path, "Definitions", "conflict.json"), "mod1 content");

        // Create second mod
        string mod2Path = Path.Combine(_modsRoot, "Mod2");
        Directory.CreateDirectory(Path.Combine(mod2Path, "Definitions"));
        File.WriteAllText(Path.Combine(mod2Path, "Definitions", "conflict.json"), "mod2 content");

        var mod1Manifest = new ModManifest
        {
            Id = "mod-1",
            Name = "Mod 1",
            Version = "1.0.0",
            DirectoryPath = mod1Path,
            Priority = 100, // Higher priority
            ContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        };

        var mod2Manifest = new ModManifest
        {
            Id = "mod-2",
            Name = "Mod 2",
            Version = "1.0.0",
            DirectoryPath = mod2Path,
            Priority = 50, // Lower priority
            ContentFolders = new Dictionary<string, string>
            {
                ["Definitions"] = "Definitions", ["Graphics"] = "Graphics"
            }
        };

        ContentProvider provider = CreateContentProvider(CreateMockModLoader(mod1Manifest, mod2Manifest));

        // Act
        string? result = provider.ResolveContentPath("Definitions", "conflict.json");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Mod1");
        result.Should().NotContain("Mod2");
        File.ReadAllText(result!).Should().Be("mod1 content");
    }

    [Fact]
    public void GetStats_TypicalUsagePattern_HighHitRate()
    {
        // Arrange - Create 100 unique test files
        ContentProvider provider = CreateContentProvider();
        var testFiles = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            string filename = $"file{i}.json";
            CreateTestFile($"Assets/Definitions/{filename}");
            testFiles.Add(filename);
        }

        // Act - Simulate typical game usage pattern
        // First pass: Access each file once (populates cache)
        foreach (string file in testFiles)
        {
            provider.ResolveContentPath("Definitions", file);
        }

        // Second pass: Access each file 9 more times (simulates repeated access)
        for (int i = 0; i < 9; i++)
        {
            foreach (string file in testFiles)
            {
                provider.ResolveContentPath("Definitions", file);
            }
        }

        ContentProviderStats stats = provider.GetStats();

        // Assert
        // Total resolutions: 100 (initial) + 900 (9 more times) = 1000
        stats.TotalResolutions.Should().Be(1000);
        // Cache misses: 100 (first access of each unique file)
        stats.CacheMisses.Should().Be(100);
        // Cache hits: 900 (all subsequent accesses)
        stats.CacheHits.Should().Be(900);
        // Hit rate: 900/1000 = 0.9 (90%)
        stats.HitRate.Should().BeGreaterOrEqualTo(0.9, "typical usage pattern should achieve at least 90% hit rate");
    }

    [Fact]
    public void GetStats_RepeatedAccess_100PercentHitRate()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/repeated.json");
        ContentProvider provider = CreateContentProvider();

        // Act - First access (cache miss)
        provider.ResolveContentPath("Definitions", "repeated.json");

        // Access the same path 99 more times (all should be cache hits)
        for (int i = 0; i < 99; i++)
        {
            provider.ResolveContentPath("Definitions", "repeated.json");
        }

        ContentProviderStats stats = provider.GetStats();

        // Assert
        stats.TotalResolutions.Should().Be(100);
        stats.CacheMisses.Should().Be(1, "only the first access should be a cache miss");
        stats.CacheHits.Should().Be(99, "all subsequent accesses should be cache hits");
        stats.HitRate.Should().Be(0.99, "hit rate should be 99% (99 hits out of 100 total)");
    }

    [Fact]
    public void GetStats_AfterInvalidation_HitRateResets()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/reset.json");
        ContentProvider provider = CreateContentProvider();

        // Act - Build up some cache hits
        provider.ResolveContentPath("Definitions", "reset.json"); // Miss
        provider.ResolveContentPath("Definitions", "reset.json"); // Hit
        provider.ResolveContentPath("Definitions", "reset.json"); // Hit

        ContentProviderStats statsBefore = provider.GetStats();

        // Invalidate cache
        provider.InvalidateCache();

        // Access again after invalidation
        provider.ResolveContentPath("Definitions", "reset.json"); // Miss (cache was cleared)
        provider.ResolveContentPath("Definitions", "reset.json"); // Hit

        ContentProviderStats statsAfter = provider.GetStats();

        // Assert
        statsBefore.TotalResolutions.Should().Be(3);
        statsBefore.CacheMisses.Should().Be(1);
        statsBefore.CacheHits.Should().Be(2);
        statsBefore.HitRate.Should().BeApproximately(0.667, 0.01);

        // After invalidation, stats continue tracking
        statsAfter.TotalResolutions.Should().Be(5, "total resolutions should continue incrementing");
        statsAfter.CacheMisses.Should().Be(2, "invalidation should cause a new cache miss");
        statsAfter.CacheHits.Should().Be(3, "cache hits should increment after re-population");
        statsAfter.HitRate.Should().Be(0.6, "hit rate should reflect all resolutions including post-invalidation");
    }

    [Fact]
    public void Cache_AtCapacity_MaintainsHitRateForRecentItems()
    {
        // Arrange - Create provider with small cache (10 items) to test LRU eviction
        IModLoader modLoader = CreateMockModLoader();
        IOptions<ContentProviderOptions> options = Options.Create(new ContentProviderOptions
        {
            BaseGameRoot = _baseGameRoot,
            MaxCacheSize = 10, // Small cache to force eviction
            LogCacheMisses = false,
            ThrowOnPathTraversal = true,
            BaseContentFolders = new Dictionary<string, string> { ["Definitions"] = "Definitions" }
        });
        var provider = new ContentProvider(modLoader, NullLogger<ContentProvider>.Instance, options);

        // Create 15 test files (more than cache capacity)
        var testFiles = new List<string>();
        for (int i = 0; i < 15; i++)
        {
            string filename = $"lru{i}.json";
            CreateTestFile($"Assets/Definitions/{filename}");
            testFiles.Add(filename);
        }

        // Act - Access all 15 files once (first 5 will be evicted due to LRU)
        foreach (string file in testFiles)
        {
            provider.ResolveContentPath("Definitions", file);
        }

        // Now repeatedly access the most recent 10 files (should all be cache hits)
        var recentFiles = testFiles.Skip(5).ToList(); // Last 10 files
        for (int i = 0; i < 10; i++)
        {
            foreach (string file in recentFiles)
            {
                provider.ResolveContentPath("Definitions", file);
            }
        }

        ContentProviderStats stats = provider.GetStats();

        // Assert
        // Initial: 15 misses (filling cache and evicting first 5)
        // Repeated access: 10 files × 10 times = 100 accesses, all should be hits
        // Total: 15 + 100 = 115 resolutions
        stats.TotalResolutions.Should().Be(115);
        stats.CacheMisses.Should().Be(15, "only initial access of each file should be a miss");
        stats.CacheHits.Should().Be(100, "all repeated accesses to cached items should be hits");

        // Hit rate for recently accessed items: 100/115 = ~87%
        stats.HitRate.Should().BeGreaterOrEqualTo(0.85,
            "LRU cache should maintain high hit rate for frequently accessed items even at capacity");
    }

    #region Phase 3: Security Audit Tests

    [Fact]
    public void ResolveContentPath_DoubleEncodedTraversal_ThrowsOrReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act - Try double-encoded path traversal (%2e = '.')
        // Note: ContentProvider does NOT URL-decode, so %2e%2e is treated as literal chars
        // This is SAFE behavior - the file won't exist, so null is returned
        string? result = provider.ResolveContentPath("Definitions", "%2e%2e%2f%2e%2e%2fetc/passwd");

        // Assert - Should safely return null (no file exists with literal % chars in name)
        result.Should().BeNull("encoded path traversal should not find any files");
    }

    [Fact]
    public void ResolveContentPath_UnicodeTraversal_ThrowsOrReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act - Try unicode variations of path traversal
        // U+002E = '.' in unicode, U+002F = '/'
        string[] unicodePaths = new[]
        {
            "\u002e\u002e\u002f\u002e\u002e\u002fetc/passwd",
            "..%c0%af..%c0%afetc/passwd", // Overlong UTF-8 encoding of '/'
            "..\u2215..\u2215etc\u2215passwd" // Division slash (U+2215)
        };

        // Assert - All should be detected as malicious
        foreach (string path in unicodePaths)
        {
            Func<string?> act = () => provider.ResolveContentPath("Definitions", path);
            act.Should().Throw<SecurityException>()
                .WithMessage("*path traversal*", $"unicode path traversal '{path}' should be detected");
        }
    }

    [Fact]
    public void ResolveContentPath_BackslashTraversal_ThrowsOrReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act - Try Windows-style backslash path traversal
        string[] windowsPaths = new[]
        {
            "..\\..\\..\\Windows\\System32\\config", "..\\etc\\passwd", "test\\..\\..\\..\\sensitive.txt"
        };

        // Assert - Should detect backslash traversal
        foreach (string path in windowsPaths)
        {
            Func<string?> act = () => provider.ResolveContentPath("Definitions", path);
            act.Should().Throw<SecurityException>()
                .WithMessage("*path traversal*", $"backslash path traversal '{path}' should be detected");
        }
    }

    [Fact]
    public void ResolveContentPath_AbsoluteWindowsPath_ThrowsOrReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act - Try absolute Windows paths
        // Note: On Linux, Windows-style paths like "C:\\" are NOT considered rooted
        // Only Unix-style absolute paths ("/etc/passwd") or UNC paths are rooted
        string[] absolutePaths = new[]
        {
            "/etc/passwd", // Unix absolute path - blocked on all platforms
            "/root/.ssh/id_rsa" // Unix absolute path - blocked
        };

        // Assert - Should reject absolute paths
        foreach (string path in absolutePaths)
        {
            Func<string?> act = () => provider.ResolveContentPath("Definitions", path);
            act.Should().Throw<SecurityException>(
                $"absolute path '{path}' should be rejected as rooted");
        }

        // Windows-style paths on Linux are treated as relative (safe - file won't exist)
        if (!OperatingSystem.IsWindows())
        {
            string[] windowsPaths = new[] { "C:\\Windows\\System32\\config", "D:\\data.txt" };
            foreach (string path in windowsPaths)
            {
                string? result = provider.ResolveContentPath("Definitions", path);
                result.Should().BeNull("Windows paths on Linux are relative and won't find files");
            }
        }
    }

    [Fact]
    public void ResolveContentPath_NullByteInjection_ThrowsOrReturnsNull()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act - Try null byte injection to bypass file extension checks
        string[] nullBytePaths = new[] { "legitimate.json\0.txt", "file.json\0../../etc/passwd", "test\0.json" };

        // Assert - Should detect and reject null byte injection
        foreach (string path in nullBytePaths)
        {
            // Null bytes should either throw SecurityException or safely return null
            // The IsPathSafe method checks for '\0' characters and blocks them
            try
            {
                string? result = provider.ResolveContentPath("Definitions", path);
                // If no exception, result should be null (blocked or file not found)
                result.Should().BeNull($"null byte injection '{path}' should be blocked");
            }
            catch (SecurityException)
            {
                // This is the expected secure behavior - null bytes trigger security exception
            }
            catch (ArgumentException)
            {
                // Also acceptable - invalid path detected
            }
        }
    }

    [Fact]
    public void ResolveContentPath_VeryLongPath_HandlesGracefully()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act - Generate extremely long path (5000+ chars)
        string longSegment = new('a', 1000);
        string veryLongPath = string.Join("/", Enumerable.Repeat(longSegment, 6)); // 6000+ chars

        // Assert - Should handle gracefully without crashing
        Func<string?> act = () => provider.ResolveContentPath("Definitions", veryLongPath);

        // Should either throw PathTooLongException, ArgumentException, or return null
        // but should NOT crash the application
        act.Should().NotThrow<OutOfMemoryException>("long paths should not cause OOM");
        act.Should().NotThrow<StackOverflowException>("long paths should not cause stack overflow");

        // It's acceptable to throw PathTooLongException or return null
        try
        {
            string? result = provider.ResolveContentPath("Definitions", veryLongPath);
            result.Should().BeNull("extremely long paths should return null if not throwing");
        }
        catch (PathTooLongException)
        {
            // This is acceptable behavior
        }
        catch (ArgumentException)
        {
            // This is also acceptable
        }
    }

    [Fact]
    public void GetAllContentPaths_SafePatterns_WorkCorrectly()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/safe.json");
        CreateTestFile("Assets/Graphics/other.json");
        ContentProvider provider = CreateContentProvider();

        // Act - Test safe pattern (*.json is the default and most common)
        // Note: Directory.EnumerateFiles doesn't support ** glob patterns
        var results = provider.GetAllContentPaths("Definitions").ToList();

        // Assert - Should find file in Definitions only
        results.Should().NotBeEmpty("safe.json was created in Definitions");
        results.Should().OnlyContain(p => p.Contains("Definitions"),
            "pattern '*.json' should only find files in Definitions");
        results.Should().NotContain(p => p.Contains("Graphics"),
            "pattern should not access Graphics directory");
    }

    [Fact]
    public void GetAllContentPaths_MaliciousPattern_ThrowsSecurityException()
    {
        // Arrange
        CreateTestFile("Assets/Definitions/safe.json");
        CreateTestFile("Assets/Graphics/sibling.json");
        ContentProvider provider = CreateContentProvider();

        // Act & Assert - Patterns with ".." are rejected to prevent directory traversal
        string[] traversalPatterns = new[]
        {
            "../*.json", // Unix-style traversal
            "..\\*.json", // Windows-style traversal
            "foo/../bar/*.json", // Embedded traversal
            "..." // Multiple dots (contains "..")
        };

        foreach (string pattern in traversalPatterns)
        {
            Func<List<string>> act = () => provider.GetAllContentPaths("Definitions", pattern).ToList();
            act.Should().Throw<SecurityException>(
                $"pattern '{pattern}' contains path traversal sequence and should be rejected");
        }
    }

    [Fact]
    public void GetAllContentPaths_AbsolutePathPattern_ThrowsSecurityException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act & Assert - Absolute path patterns are rejected
        string[] absolutePatterns = new[]
        {
            "/etc/*.json", // Unix absolute
            "C:\\*.json" // Windows absolute (on Windows this is rooted)
        };

        foreach (string pattern in absolutePatterns)
        {
            // Only test if the pattern is actually rooted on the current platform
            if (Path.IsPathRooted(pattern))
            {
                Func<List<string>> act = () => provider.GetAllContentPaths("Definitions", pattern).ToList();
                act.Should().Throw<SecurityException>(
                    $"absolute path pattern '{pattern}' should be rejected");
            }
        }
    }

    [Fact]
    public void GetAllContentPaths_NullBytePattern_ThrowsSecurityException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act & Assert - Null byte injection is rejected
        Func<List<string>> act = () => provider.GetAllContentPaths("Definitions", "*.json\0.txt").ToList();
        act.Should().Throw<SecurityException>(
            "patterns with null bytes should be rejected");
    }

    [Fact]
    public void GetAllContentPaths_LeadingSeparatorPattern_ThrowsSecurityException()
    {
        // Arrange
        ContentProvider provider = CreateContentProvider();

        // Act & Assert - Patterns starting with separator are rejected
        Func<List<string>> act = () => provider.GetAllContentPaths("Definitions", "/subdir/*.json").ToList();
        act.Should().Throw<SecurityException>(
            "patterns starting with directory separator should be rejected");
    }

    #endregion
}
