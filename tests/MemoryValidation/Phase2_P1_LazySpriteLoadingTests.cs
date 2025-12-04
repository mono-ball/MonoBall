using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Engine.Rendering.Assets;
using MonoBallFramework.Game.Services;
using MonoBallFramework.Game.Systems;
using Xunit;
using Xunit.Abstractions;

namespace MonoBallFramework.Tests.MemoryValidation
{
    /// <summary>
    /// PHASE 2 CRITICAL TEST P1: Memory Reduction Validation
    /// Validates lazy sprite loading reduces memory by 25-35MB compared to loading all sprites.
    /// Tests the new SpriteTextureLoader.LoadSpritesForMapAsync() approach vs old behavior.
    /// </summary>
    public class Phase2_P1_LazySpriteLoadingTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private const long EXPECTED_MEMORY_SAVINGS_MB = 25;
        private const string SPRITES_BASE_PATH = "Assets/Sprites";

        // Test uses a mock GraphicsDevice for texture loading
        private GraphicsDevice? _graphicsDevice;

        public Phase2_P1_LazySpriteLoadingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Priority", "Critical")]
        public async Task LazyLoading_ReducesMemoryBy25MB()
        {
            // ARRANGE
            _output.WriteLine("=== PHASE 2 TEST P1: LAZY SPRITE LOADING MEMORY VALIDATION ===");
            _output.WriteLine(
                $"Target: ≥{EXPECTED_MEMORY_SAVINGS_MB}MB reduction vs loading all sprites"
            );
            _output.WriteLine("");

            // Initialize test components
            var assetManager = InitializeAssetManager();
            var spriteLoader = new SpriteLoader(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SpriteLoader>.Instance
            );

            // Scan available sprite files
            var allSpriteFiles = DiscoverAllSpriteFiles();
            _output.WriteLine(
                $"Found {allSpriteFiles.Count} total sprite files in {SPRITES_BASE_PATH}"
            );
            _output.WriteLine("");

            // ========================================
            // BASELINE: Simulate OLD system (load ALL sprites)
            // ========================================
            _output.WriteLine("--- BASELINE: Loading ALL sprites (old behavior) ---");
            ForceGarbageCollection();
            var baselineStartMemoryMB = GetCurrentMemoryMB();
            _output.WriteLine($"Memory before loading: {baselineStartMemoryMB:F1}MB");

            var baselineTexturesLoaded = await LoadAllSpritesBaseline(assetManager, allSpriteFiles);
            ForceGarbageCollection();

            var baselineEndMemoryMB = GetCurrentMemoryMB();
            var baselineMemoryUsedMB = baselineEndMemoryMB - baselineStartMemoryMB;

            _output.WriteLine($"Loaded {baselineTexturesLoaded} sprite textures");
            _output.WriteLine($"Memory after loading: {baselineEndMemoryMB:F1}MB");
            _output.WriteLine($"Memory consumed by ALL sprites: {baselineMemoryUsedMB:F1}MB");
            _output.WriteLine("");

            // Cleanup baseline textures
            CleanupAllTextures(assetManager);
            ForceGarbageCollection();

            // ========================================
            // NEW SYSTEM: Load ONLY one map's sprites (lazy loading)
            // ========================================
            _output.WriteLine("--- NEW SYSTEM: Lazy loading for ONE map ---");

            // Reinitialize AssetManager after cleanup
            assetManager = InitializeAssetManager();

            ForceGarbageCollection();
            var lazyStartMemoryMB = GetCurrentMemoryMB();
            _output.WriteLine($"Memory before lazy load: {lazyStartMemoryMB:F1}MB");

            // Use the new SpriteTextureLoader with LoadSpritesForMapAsync
            var spriteTextureLoader = new SpriteTextureLoader(
                spriteLoader,
                assetManager,
                _graphicsDevice!,
                SPRITES_BASE_PATH,
                logger: null
            );

            // Simulate loading sprites for a single map (e.g., map ID 1)
            var testMapId = 1;
            var testMapSpriteIds = GetTestMapSpriteIds(); // Small subset of sprites for one map

            _output.WriteLine(
                $"Loading sprites for map {testMapId}: {testMapSpriteIds.Count} sprites"
            );
            var loadedSpriteKeys = await spriteTextureLoader.LoadSpritesForMapAsync(
                testMapId,
                testMapSpriteIds
            );
            ForceGarbageCollection();

            var lazyEndMemoryMB = GetCurrentMemoryMB();
            var lazyMemoryUsedMB = lazyEndMemoryMB - lazyStartMemoryMB;

            _output.WriteLine($"Loaded {loadedSpriteKeys.Count} sprite textures for map");
            _output.WriteLine($"Memory after lazy load: {lazyEndMemoryMB:F1}MB");
            _output.WriteLine($"Memory consumed by ONE map's sprites: {lazyMemoryUsedMB:F1}MB");
            _output.WriteLine("");

            // ========================================
            // CALCULATE SAVINGS
            // ========================================
            var memorySavingsMB = baselineMemoryUsedMB - lazyMemoryUsedMB;
            var savingsPercentage = (memorySavingsMB / baselineMemoryUsedMB) * 100.0;

            _output.WriteLine("=== RESULTS ===");
            _output.WriteLine($"Baseline Memory (ALL sprites): {baselineMemoryUsedMB:F1}MB");
            _output.WriteLine($"Lazy Load Memory (ONE map):    {lazyMemoryUsedMB:F1}MB");
            _output.WriteLine(
                $"Memory Savings:                {memorySavingsMB:F1}MB ({savingsPercentage:F1}%)"
            );
            _output.WriteLine($"Target Savings:                ≥{EXPECTED_MEMORY_SAVINGS_MB}MB");
            _output.WriteLine("");

            // ASSERT
            Assert.True(
                memorySavingsMB >= EXPECTED_MEMORY_SAVINGS_MB,
                $"FAIL: Lazy loading saved {memorySavingsMB:F1}MB, which is less than the target {EXPECTED_MEMORY_SAVINGS_MB}MB reduction. "
                    + $"Expected ≥{EXPECTED_MEMORY_SAVINGS_MB}MB savings, got {memorySavingsMB:F1}MB."
            );

            _output.WriteLine(
                $"✅ PASS: Lazy loading reduced memory by {memorySavingsMB:F1}MB (≥{EXPECTED_MEMORY_SAVINGS_MB}MB target)"
            );
            _output.WriteLine($"Phase 2 optimization successfully validated!");
        }

        // ========================================
        // HELPER METHODS
        // ========================================

        /// <summary>
        /// Initializes a mock AssetManager for testing.
        /// Creates a test GraphicsDevice for texture loading.
        /// </summary>
        private AssetManager InitializeAssetManager()
        {
            try
            {
                // Create a headless GraphicsDevice for testing
                var presentationParameters = new PresentationParameters
                {
                    BackBufferWidth = 800,
                    BackBufferHeight = 600,
                    BackBufferFormat = SurfaceFormat.Color,
                    DepthStencilFormat = DepthFormat.Depth24,
                    DeviceWindowHandle = IntPtr.Zero,
                    IsFullScreen = false,
                };

                _graphicsDevice = new GraphicsDevice(
                    GraphicsAdapter.DefaultAdapter,
                    GraphicsProfile.HiDef,
                    presentationParameters
                );

                return new AssetManager(_graphicsDevice, "MonoBallFramework.Game/Assets");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"WARNING: Could not create GraphicsDevice: {ex.Message}");
                _output.WriteLine("Test will run with limited graphics functionality");
                throw;
            }
        }

        /// <summary>
        /// Discovers all sprite PNG files in the Assets/Sprites directory.
        /// </summary>
        private List<string> DiscoverAllSpriteFiles()
        {
            var spritesPath = Path.Combine("MonoBallFramework.Game", SPRITES_BASE_PATH);

            if (!Directory.Exists(spritesPath))
            {
                // Try alternate path (bin/Release or bin/Debug)
                spritesPath = Path.Combine(
                    "MonoBallFramework.Game",
                    "bin",
                    "Release",
                    "net9.0",
                    SPRITES_BASE_PATH
                );
            }

            if (!Directory.Exists(spritesPath))
            {
                _output.WriteLine($"WARNING: Sprites directory not found: {spritesPath}");
                return new List<string>();
            }

            var spriteFiles = Directory
                .GetFiles(spritesPath, "spritesheet.png", SearchOption.AllDirectories)
                .ToList();

            return spriteFiles;
        }

        /// <summary>
        /// Loads ALL sprite textures (simulates old system behavior).
        /// </summary>
        private async Task<int> LoadAllSpritesBaseline(
            AssetManager assetManager,
            List<string> spriteFiles
        )
        {
            var loadedCount = 0;

            foreach (var spriteFile in spriteFiles)
            {
                try
                {
                    // Generate a unique texture key
                    var relativePath = GetRelativePath(spriteFile);
                    var textureKey =
                        $"baseline_{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";

                    // Load texture via AssetManager
                    if (File.Exists(spriteFile) && _graphicsDevice != null)
                    {
                        using var fileStream = File.OpenRead(spriteFile);
                        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);
                        assetManager.RegisterTexture(textureKey, texture);
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Failed to load sprite {spriteFile}: {ex.Message}");
                }
            }

            // Wait for async operations to complete
            await Task.Delay(100);
            return loadedCount;
        }

        /// <summary>
        /// Gets a relative path from a full file path.
        /// </summary>
        private string GetRelativePath(string fullPath)
        {
            // Extract path relative to Assets/Sprites
            var spritesIndex = fullPath.IndexOf("Sprites", StringComparison.OrdinalIgnoreCase);
            if (spritesIndex >= 0)
            {
                return fullPath.Substring(spritesIndex);
            }
            return Path.GetFileName(fullPath);
        }

        /// <summary>
        /// Gets a test set of sprite IDs for a single map (simulates one map's NPCs).
        /// Format: "category/spriteName"
        /// </summary>
        private List<string> GetTestMapSpriteIds()
        {
            // Simulate a small map with 5-10 NPCs (e.g., Littleroot Town or Pallet Town)
            return new List<string>
            {
                "generic/mom",
                "generic/prof_birch",
                "generic/boy_1",
                "generic/girl_1",
                "generic/man_1",
                "generic/woman_1",
                "generic/old_man",
                "generic/nurse",
            };
        }

        /// <summary>
        /// Cleans up all loaded textures from the asset manager.
        /// </summary>
        private void CleanupAllTextures(AssetManager assetManager)
        {
            // AssetManager.Dispose() will clear all textures
            assetManager.Dispose();
        }

        /// <summary>
        /// Forces aggressive garbage collection for accurate memory measurements.
        /// </summary>
        private void ForceGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            Thread.Sleep(100); // Allow cleanup to complete
        }

        /// <summary>
        /// Gets current memory usage in megabytes.
        /// </summary>
        private double GetCurrentMemoryMB()
        {
            return GC.GetTotalMemory(false) / 1_000_000.0;
        }

        public void Dispose()
        {
            // Cleanup
            _graphicsDevice?.Dispose();
            ForceGarbageCollection();
        }
    }
}
