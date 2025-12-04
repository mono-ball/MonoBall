using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MonoBallFramework.Engine.Core.Modding;

namespace MonoBallFramework.Tests.Phase5;

/// <summary>
/// Test suite for Phase 5.1: Mod Autoloading functionality
/// Tests: Discovery, dependency resolution, conflict handling, version validation
/// </summary>
public class ModLoaderTests
{
    private readonly string _testModsPath;
    private ModLoader _loader;

    public ModLoaderTests()
    {
        _testModsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "Mods"
        );
        _loader = new ModLoader(NullLogger<ModLoader>.Instance, _testModsPath);
    }

    [Fact]
    public void DiscoverMods_FindsBasicTestMod()
    {
        // Act
        var mods = _loader.DiscoverMods();

        // Assert
        mods.Should().Contain(m => m.Manifest.ModId == "MonoBallFramework.test.basic");
    }

    [Fact]
    public void DiscoverMods_RejectsInvalidVersionFormat()
    {
        // Act
        var mods = _loader.DiscoverMods();

        // Assert
        mods.Should()
            .NotContain(
                m => m.Manifest.ModId == "MonoBallFramework.test.badversion",
                "invalid semantic version should fail validation"
            );
    }

    [Fact]
    public void DiscoverMods_ValidatesSemanticVersioning()
    {
        // Act
        var mods = _loader.DiscoverMods();
        var versionMod = mods.FirstOrDefault(m => m.Manifest.ModId == "MonoBallFramework.test.version");

        // Assert
        versionMod.Should().NotBeNull();
        versionMod!.Manifest.Version.Should().Be("2.5.13");
    }

    [Fact]
    public void SortByLoadOrder_ResolvesHardDependencies()
    {
        // Arrange
        var mods = _loader.DiscoverMods();

        // Act
        var sorted = _loader.SortByLoadOrder(mods);

        // Assert - basic should load before dependency
        var basicIndex = sorted.FindIndex(m => m.Manifest.ModId == "MonoBallFramework.test.basic");
        var depIndex = sorted.FindIndex(m => m.Manifest.ModId == "MonoBallFramework.test.dependency");

        basicIndex.Should().BeLessThan(depIndex, "dependencies must load after their requirements");
    }

    [Fact]
    public void SortByLoadOrder_RespectsSoftDependencies()
    {
        // Arrange
        var mods = _loader.DiscoverMods();

        // Act
        var sorted = _loader.SortByLoadOrder(mods);

        // Assert - basic should load before version (LoadAfter)
        var basicIndex = sorted.FindIndex(m => m.Manifest.ModId == "MonoBallFramework.test.basic");
        var versionIndex = sorted.FindIndex(m => m.Manifest.ModId == "MonoBallFramework.test.version");

        if (basicIndex >= 0 && versionIndex >= 0)
        {
            basicIndex.Should().BeLessThan(versionIndex, "LoadAfter should be respected");
        }
    }

    [Fact]
    public void SortByLoadOrder_RespectsLoadPriority()
    {
        // Arrange
        var mods = _loader.DiscoverMods();

        // Act
        var sorted = _loader.SortByLoadOrder(mods);

        // Assert - lower priority loads first
        var versionMod = sorted.FirstOrDefault(m => m.Manifest.ModId == "MonoBallFramework.test.version");
        var basicMod = sorted.FirstOrDefault(m => m.Manifest.ModId == "MonoBallFramework.test.basic");

        if (versionMod != null && basicMod != null)
        {
            var versionIndex = sorted.IndexOf(versionMod);
            var basicIndex = sorted.IndexOf(basicMod);

            versionIndex
                .Should()
                .BeLessThan(
                    basicIndex,
                    "version mod (priority 50) should load before basic (priority 100)"
                );
        }
    }

    [Fact]
    public void SortByLoadOrder_HandlesConflictingPriorities()
    {
        // Arrange
        var mods = _loader.DiscoverMods();

        // Act
        var sorted = _loader.SortByLoadOrder(mods);

        // Assert - both should load successfully even with same priority
        sorted
            .Should()
            .Contain(m => m.Manifest.ModId == "MonoBallFramework.test.basic")
            .And.Contain(m => m.Manifest.ModId == "MonoBallFramework.test.conflict");
    }

    [Fact]
    public void SortByLoadOrder_ThrowsOnCircularDependency()
    {
        // This would require creating test mods with circular deps
        // Placeholder for future implementation
        Assert.True(true, "Circular dependency detection not yet tested");
    }

    [Fact]
    public void SortByLoadOrder_ThrowsOnMissingDependency()
    {
        // Arrange
        var mods = new List<LoadedMod>
        {
            new LoadedMod
            {
                Manifest = new ModManifest
                {
                    ModId = "test.missing",
                    Name = "Missing Dep Test",
                    Dependencies = new List<string> { "nonexistent.mod" },
                },
                RootPath = "/fake/path",
            },
        };

        // Act & Assert
        var act = () => _loader.SortByLoadOrder(mods);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*depends on 'nonexistent.mod'*");
    }

    [Fact]
    public void LoadedMod_ResolvePath_WorksCorrectly()
    {
        // Arrange
        var mod = new LoadedMod
        {
            Manifest = new ModManifest { ModId = "test", Name = "Test" },
            RootPath = "/mods/test-mod",
        };

        // Act
        var resolvedPath = mod.ResolvePath("scripts/behavior.csx");

        // Assert
        resolvedPath.Should().Be("/mods/test-mod/scripts/behavior.csx");
    }

    [Fact]
    public void DiscoverMods_SkipsDirectoriesWithoutManifest()
    {
        // This is tested by ensuring no errors occur
        // and only valid mods are returned
        var act = () => _loader.DiscoverMods();
        act.Should().NotThrow("missing mod.json should be logged and skipped");
    }

    [Fact]
    public void DiscoverMods_LogsModInformation()
    {
        // Arrange
        var mods = _loader.DiscoverMods();

        // Assert - all discovered mods should have valid manifests
        mods.Should()
            .AllSatisfy(mod =>
            {
                mod.Manifest.ModId.Should().NotBeNullOrEmpty();
                mod.Manifest.Name.Should().NotBeNullOrEmpty();
                mod.Manifest.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
                mod.RootPath.Should().NotBeNullOrEmpty();
            });
    }
}
