using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Rendering.Assets;

namespace PokeSharp.Game.Diagnostics;

/// <summary>
/// Diagnostic utility to check asset loading.
/// </summary>
public static class AssetDiagnostics
{
    public static void PrintAssetManagerStatus(AssetManager assetManager)
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║     ASSET MANAGER DIAGNOSTIC REPORT      ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine($"Total Loaded Textures: {assetManager.LoadedTextureCount}");
        Console.WriteLine();

        // Check for player texture
        Console.WriteLine("🔍 Checking for 'player' texture:");
        bool hasPlayer = assetManager.HasTexture("player");
        Console.WriteLine($"   HasTexture('player'): {hasPlayer}");

        if (hasPlayer)
        {
            try
            {
                var playerTexture = assetManager.GetTexture("player");
                Console.WriteLine($"   ✅ Player texture loaded successfully!");
                Console.WriteLine($"   Dimensions: {playerTexture.Width}x{playerTexture.Height}px");
                Console.WriteLine($"   Format: {playerTexture.Format}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ ERROR getting player texture: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"   ❌ Player texture NOT found in AssetManager!");
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
    }
}
