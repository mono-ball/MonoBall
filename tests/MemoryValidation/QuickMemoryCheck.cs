using System;
using System.Threading;

namespace PokeSharp.Tests.MemoryValidation
{
    /// <summary>
    /// Quick memory check utility for manual testing during development
    /// Add LogMemoryStats() calls to PokeSharpGame.cs to track memory in real-time
    /// </summary>
    public static class QuickMemoryCheck
    {
        /// <summary>
        /// Call this method to log current memory statistics
        /// Add to PokeSharpGame.cs Update() method or after map loads
        /// </summary>
        public static void LogMemoryStats(object assetManager = null)
        {
            var memMB = GC.GetTotalMemory(false) / 1_000_000.0;

            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║         MEMORY STATISTICS              ║");
            Console.WriteLine("╠════════════════════════════════════════╣");
            Console.WriteLine($"║ Total Memory:     {memMB, 10:F1} MB      ║");

            // If AssetManager is provided, show cache stats
            if (assetManager != null)
            {
                // Use reflection to get cache stats
                var type = assetManager.GetType();
                var cacheProp = type.GetProperty("TextureCacheSizeBytes");
                var textureProp = type.GetProperty("LoadedTextureCount");

                if (cacheProp != null && textureProp != null)
                {
                    var cacheBytes = (long)cacheProp.GetValue(assetManager);
                    var textures = (int)textureProp.GetValue(assetManager);
                    var cacheMB = cacheBytes / 1_000_000.0;

                    Console.WriteLine($"║ Texture Cache:    {cacheMB, 10:F1} MB      ║");
                    Console.WriteLine($"║ Loaded Textures:  {textures, 10}         ║");
                }
            }

            Console.WriteLine("╠════════════════════════════════════════╣");
            Console.WriteLine($"║ Status: {GetMemoryStatus(memMB), 28} ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
        }

        /// <summary>
        /// Get memory status based on thresholds
        /// </summary>
        private static string GetMemoryStatus(double memoryMB)
        {
            if (memoryMB < 300)
                return "✅ EXCELLENT (<300MB)";
            else if (memoryMB < 400)
                return "✅ GOOD (<400MB)";
            else if (memoryMB < 500)
                return "⚠️  ACCEPTABLE (<500MB)";
            else if (memoryMB < 600)
                return "⚠️  WARNING (>500MB)";
            else
                return "❌ CRITICAL (>600MB)";
        }

        /// <summary>
        /// Run a quick memory test scenario
        /// Useful for testing in Program.Main or a test harness
        /// </summary>
        public static void RunQuickTest()
        {
            Console.WriteLine("\n🧪 Running Quick Memory Test...\n");

            // Baseline
            ForceGC();
            Console.WriteLine("1️⃣  BASELINE:");
            LogMemoryStats();
            Thread.Sleep(1000);

            // Simulate game init
            Console.WriteLine("\n2️⃣  AFTER GAME INIT:");
            // Simulate some allocations
            var data = new byte[10_000_000]; // 10MB allocation
            LogMemoryStats();
            Thread.Sleep(1000);

            // Cleanup
            Console.WriteLine("\n3️⃣  AFTER CLEANUP:");
            data = null;
            ForceGC();
            LogMemoryStats();

            Console.WriteLine("\n✅ Quick test complete!\n");
        }

        /// <summary>
        /// Force garbage collection for accurate memory readings
        /// </summary>
        public static void ForceGC()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            Thread.Sleep(100);
        }

        /// <summary>
        /// Monitor memory in real-time (call in game loop)
        /// </summary>
        public static void MonitorMemory(object assetManager, int frameCount)
        {
            // Log every 5 seconds (assuming 60 FPS)
            if (frameCount % 300 == 0)
            {
                Console.WriteLine($"\n[Frame {frameCount}]");
                LogMemoryStats(assetManager);
            }
        }
    }
}
