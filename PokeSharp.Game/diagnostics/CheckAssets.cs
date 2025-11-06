using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Rendering.Assets;

namespace PokeSharp.Game.Diagnostics;

/// <summary>
///     Diagnostic utility to check asset loading.
/// </summary>
public static class AssetDiagnostics
{
    public static void PrintAssetManagerStatus(AssetManager assetManager, ILogger? logger = null)
    {
        if (logger == null)
            return;

        logger.LogDiagnosticHeader("ASSET MANAGER DIAGNOSTIC REPORT");

        logger.LogDiagnosticInfo("Total Loaded Textures", assetManager.LoadedTextureCount);

        // Check for player texture
        var hasPlayer = assetManager.HasTexture("player");
        if (hasPlayer)
            try
            {
                var playerTexture = assetManager.GetTexture("player");
                logger.LogResourceLoaded(
                    "Texture",
                    "player",
                    ("dimensions", $"{playerTexture.Width}x{playerTexture.Height}px"),
                    ("format", playerTexture.Format.ToString())
                );
            }
            catch (Exception ex)
            {
                logger.LogCriticalError(ex, "Get player texture");
            }
        else
            logger.LogResourceNotFound("Texture", "player");

        logger.LogDiagnosticSeparator();
    }
}
