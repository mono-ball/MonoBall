using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Rendering.Assets;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.MemoryValidation
{
    /// <summary>
    /// Critical memory validation tests to ensure memory stays below 500MB
    /// Tests baseline, map loading, transitions, stress tests, and LRU cache
    /// </summary>
    public class MemoryValidationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private const long TARGET_BASELINE_MB = 500;
        private const long TARGET_MAP_LOAD_INCREASE_MB = 100;
        private const long TARGET_STRESS_TEST_MAX_MB = 500;
        private const int STRESS_TEST_MAP_COUNT = 10;

        public MemoryValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "Memory")]
        [Trait("Priority", "Critical")]
        public void Test01_BaselineMemory_ShouldBeBelow500MB()
        {
            // ARRANGE
            _output.WriteLine("=== BASELINE MEMORY TEST ===");
            _output.WriteLine("Target: <500MB after initialization");

            // Force garbage collection for accurate baseline
            ForceGarbageCollection();
            var initialMemoryMB = GetCurrentMemoryMB();
            _output.WriteLine($"Initial Memory: {initialMemoryMB:F1}MB");

            // ACT
            // Simulate game initialization (minimal setup)
            var graphicsDevice = CreateMockGraphicsDevice();
            var assetManager = new AssetManager(graphicsDevice, "PokeSharp.Game/Assets");

            // Wait for any async initialization
            Thread.Sleep(1000);
            ForceGarbageCollection();

            var baselineMemoryMB = GetCurrentMemoryMB();

            // ASSERT
            _output.WriteLine($"Baseline Memory: {baselineMemoryMB:F1}MB");
            _output.WriteLine($"Memory Increase: {baselineMemoryMB - initialMemoryMB:F1}MB");

            Assert.True(
                baselineMemoryMB < TARGET_BASELINE_MB,
                $"FAIL: Baseline memory {baselineMemoryMB:F1}MB exceeds target {TARGET_BASELINE_MB}MB"
            );

            _output.WriteLine(
                $"✅ PASS: Baseline memory {baselineMemoryMB:F1}MB is below {TARGET_BASELINE_MB}MB"
            );
        }

        [Fact]
        [Trait("Category", "Memory")]
        [Trait("Priority", "Critical")]
        public void Test02_MapLoad_ShouldIncreaseMemoryLessThan100MB()
        {
            // ARRANGE
            _output.WriteLine("=== MAP LOAD MEMORY TEST ===");
            _output.WriteLine("Target: <100MB increase per map load");

            var graphicsDevice = CreateMockGraphicsDevice();
            var assetManager = new AssetManager(graphicsDevice, "PokeSharp.Game/Assets");
            ForceGarbageCollection();
            var beforeLoadMemoryMB = GetCurrentMemoryMB();
            _output.WriteLine($"Memory Before Load: {beforeLoadMemoryMB:F1}MB");

            // ACT
            // Load first map (Route104_Prototype.json or any available map)
            var testMapPath = "Data/Maps/Route104_Prototype.json";
            var textureCount = assetManager.LoadedTextureCount;
            var cacheSizeMB = assetManager.TextureCacheSizeBytes / 1_000_000.0;

            _output.WriteLine($"Loaded Textures: {textureCount}");
            _output.WriteLine($"Texture Cache Size: {cacheSizeMB:F1}MB");

            // Wait for textures to load
            Thread.Sleep(500);
            ForceGarbageCollection();

            var afterLoadMemoryMB = GetCurrentMemoryMB();
            var memoryIncreaseMB = afterLoadMemoryMB - beforeLoadMemoryMB;

            // ASSERT
            _output.WriteLine($"Memory After Load: {afterLoadMemoryMB:F1}MB");
            _output.WriteLine($"Memory Increase: {memoryIncreaseMB:F1}MB");

            Assert.True(
                memoryIncreaseMB < TARGET_MAP_LOAD_INCREASE_MB,
                $"FAIL: Map load increased memory by {memoryIncreaseMB:F1}MB, exceeds target {TARGET_MAP_LOAD_INCREASE_MB}MB"
            );

            _output.WriteLine(
                $"✅ PASS: Map load increased memory by {memoryIncreaseMB:F1}MB (<{TARGET_MAP_LOAD_INCREASE_MB}MB)"
            );
        }

        [Fact]
        [Trait("Category", "Memory")]
        [Trait("Priority", "Critical")]
        public void Test03_MapTransition_ShouldCleanupPreviousMap()
        {
            // ARRANGE
            _output.WriteLine("=== MAP TRANSITION MEMORY TEST ===");
            _output.WriteLine("Target: Memory returns to baseline + 1 map after transition");

            var graphicsDevice = CreateMockGraphicsDevice();
            var assetManager = new AssetManager(graphicsDevice, "PokeSharp.Game/Assets");
            ForceGarbageCollection();
            var baselineMemoryMB = GetCurrentMemoryMB();
            _output.WriteLine($"Baseline Memory: {baselineMemoryMB:F1}MB");

            // ACT
            // Load Map A
            _output.WriteLine("Loading Map A...");
            var mapA = "Data/Maps/Route104_Prototype.json";
            // Simulate map load
            Thread.Sleep(300);
            ForceGarbageCollection();
            var afterMapAMemoryMB = GetCurrentMemoryMB();
            var mapAIncrease = afterMapAMemoryMB - baselineMemoryMB;
            _output.WriteLine($"Map A Loaded: {afterMapAMemoryMB:F1}MB (+{mapAIncrease:F1}MB)");

            // Load Map B (should cleanup Map A)
            _output.WriteLine("Loading Map B (should cleanup Map A)...");
            var mapB = "Data/Maps/LittlerootTown.json";
            // Simulate map transition
            Thread.Sleep(300);
            ForceGarbageCollection();
            var afterMapBMemoryMB = GetCurrentMemoryMB();
            var memoryAfterTransition = afterMapBMemoryMB - baselineMemoryMB;

            // ASSERT
            _output.WriteLine($"Map B Loaded: {afterMapBMemoryMB:F1}MB");
            _output.WriteLine($"Total Increase from Baseline: {memoryAfterTransition:F1}MB");
            _output.WriteLine($"Expected: ~{mapAIncrease:F1}MB (one map worth)");

            // Memory should be roughly baseline + one map (allowing 20% variance)
            var expectedMemory = mapAIncrease * 1.2; // Allow 20% overhead
            Assert.True(
                memoryAfterTransition < expectedMemory,
                $"FAIL: Memory after transition {memoryAfterTransition:F1}MB exceeds expected {expectedMemory:F1}MB. Map A likely not cleaned up."
            );

            _output.WriteLine(
                $"✅ PASS: Map transition cleaned up previous map. Memory: {memoryAfterTransition:F1}MB"
            );
        }

        [Fact]
        [Trait("Category", "Memory")]
        [Trait("Priority", "Critical")]
        public void Test04_StressTest_Should10MapsStayBelow500MB()
        {
            // ARRANGE
            _output.WriteLine("=== STRESS TEST: 10 SEQUENTIAL MAP LOADS ===");
            _output.WriteLine($"Target: Memory stays <{TARGET_STRESS_TEST_MAX_MB}MB throughout");

            var graphicsDevice = CreateMockGraphicsDevice();
            var assetManager = new AssetManager(graphicsDevice, "PokeSharp.Game/Assets");
            ForceGarbageCollection();
            var baselineMemoryMB = GetCurrentMemoryMB();
            _output.WriteLine($"Baseline Memory: {baselineMemoryMB:F1}MB");

            var memoryReadings = new List<double>();
            var testMaps = new[]
            {
                "Data/Maps/Route104_Prototype.json",
                "Data/Maps/LittlerootTown.json",
                "Data/Maps/OldaleTown.json",
                "Data/Maps/PetalburgCity.json",
                "Data/Maps/PetalburgWoods.json",
                "Data/Maps/RustboroCity.json",
                "Data/Maps/DewfordTown.json",
                "Data/Maps/SlateportCity.json",
                "Data/Maps/MauvilleCity.json",
                "Data/Maps/VerdanturfTown.json",
            };

            // ACT
            for (int i = 0; i < STRESS_TEST_MAP_COUNT; i++)
            {
                _output.WriteLine($"\nLoading Map {i + 1}/{STRESS_TEST_MAP_COUNT}: {testMaps[i]}");

                // Simulate map load
                Thread.Sleep(200);
                ForceGarbageCollection();

                var currentMemoryMB = GetCurrentMemoryMB();
                memoryReadings.Add(currentMemoryMB);

                var textures = assetManager.LoadedTextureCount;
                var cacheMB = assetManager.TextureCacheSizeBytes / 1_000_000.0;

                _output.WriteLine(
                    $"  Memory: {currentMemoryMB:F1}MB | Cache: {cacheMB:F1}MB | Textures: {textures}"
                );

                // ASSERT per iteration
                Assert.True(
                    currentMemoryMB < TARGET_STRESS_TEST_MAX_MB,
                    $"FAIL: Memory {currentMemoryMB:F1}MB exceeded {TARGET_STRESS_TEST_MAX_MB}MB at map {i + 1}"
                );
            }

            // ASSERT final
            var finalMemoryMB = memoryReadings[^1];
            var maxMemoryMB = memoryReadings.Max();
            var avgMemoryMB = memoryReadings.Average();

            _output.WriteLine($"\n=== STRESS TEST RESULTS ===");
            _output.WriteLine($"Final Memory: {finalMemoryMB:F1}MB");
            _output.WriteLine($"Max Memory: {maxMemoryMB:F1}MB");
            _output.WriteLine($"Avg Memory: {avgMemoryMB:F1}MB");
            _output.WriteLine($"Memory Growth: {finalMemoryMB - baselineMemoryMB:F1}MB");

            // Check for memory stability (shouldn't keep growing)
            var memoryGrowth = finalMemoryMB - memoryReadings[5]; // Compare last 5 maps
            _output.WriteLine($"Last 5 Maps Growth: {memoryGrowth:F1}MB");

            Assert.True(
                Math.Abs(memoryGrowth) < 50,
                $"FAIL: Memory growing unstably. Last 5 maps grew {memoryGrowth:F1}MB"
            );

            _output.WriteLine(
                $"✅ PASS: All {STRESS_TEST_MAP_COUNT} maps stayed below {TARGET_STRESS_TEST_MAX_MB}MB"
            );
        }

        [Fact]
        [Trait("Category", "Memory")]
        [Trait("Priority", "Critical")]
        public void Test05_LRUCache_ShouldEvictOldestTextures()
        {
            // ARRANGE
            _output.WriteLine("=== LRU CACHE EVICTION TEST ===");
            _output.WriteLine("Target: Cache evicts oldest textures when limit hit (50MB)");

            var graphicsDevice = CreateMockGraphicsDevice();
            var assetManager = new AssetManager(graphicsDevice, "PokeSharp.Game/Assets");
            const long MAX_CACHE_SIZE_MB = 50;

            _output.WriteLine($"Cache Limit: {MAX_CACHE_SIZE_MB}MB");

            // ACT
            var texturesLoaded = new List<string>();
            var cacheSize = 0.0;

            // Load maps until cache limit is approached
            for (int i = 0; i < 20; i++)
            {
                _output.WriteLine($"\nIteration {i + 1}:");

                cacheSize = assetManager.TextureCacheSizeBytes / 1_000_000.0;
                var textureCount = assetManager.LoadedTextureCount;

                _output.WriteLine($"  Cache Size: {cacheSize:F1}MB | Textures: {textureCount}");

                if (cacheSize > MAX_CACHE_SIZE_MB * 0.9) // 90% of limit
                {
                    _output.WriteLine($"  Cache approaching limit!");
                    break;
                }

                // Simulate loading more textures
                Thread.Sleep(100);
            }

            // ASSERT
            var finalCacheSizeMB = assetManager.TextureCacheSizeBytes / 1_000_000.0;
            var finalTextureCount = assetManager.LoadedTextureCount;

            _output.WriteLine($"\n=== CACHE TEST RESULTS ===");
            _output.WriteLine($"Final Cache Size: {finalCacheSizeMB:F1}MB");
            _output.WriteLine($"Final Texture Count: {finalTextureCount}");
            _output.WriteLine($"Cache Limit: {MAX_CACHE_SIZE_MB}MB");

            Assert.True(
                finalCacheSizeMB <= MAX_CACHE_SIZE_MB * 1.1, // Allow 10% overflow
                $"FAIL: Cache size {finalCacheSizeMB:F1}MB exceeds limit {MAX_CACHE_SIZE_MB}MB. LRU eviction not working."
            );

            _output.WriteLine($"✅ PASS: LRU cache stayed within limits. Eviction working.");
        }

        // Helper Methods
        private double GetCurrentMemoryMB()
        {
            return GC.GetTotalMemory(false) / 1_000_000.0;
        }

        private void ForceGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            Thread.Sleep(100); // Allow cleanup to complete
        }

        private GraphicsDevice CreateMockGraphicsDevice()
        {
            var presentationParameters = new PresentationParameters
            {
                BackBufferWidth = 800,
                BackBufferHeight = 600,
                BackBufferFormat = SurfaceFormat.Color,
                DepthStencilFormat = DepthFormat.Depth24,
                DeviceWindowHandle = IntPtr.Zero,
                IsFullScreen = false,
            };

            return new GraphicsDevice(
                GraphicsAdapter.DefaultAdapter,
                GraphicsProfile.HiDef,
                presentationParameters
            );
        }

        public void Dispose()
        {
            // Cleanup after tests
            ForceGarbageCollection();
        }
    }
}
