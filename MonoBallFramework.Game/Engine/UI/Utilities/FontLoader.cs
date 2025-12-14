using FontStashSharp;

namespace MonoBallFramework.Game.Engine.UI.Utilities;

/// <summary>
///     Utility for loading game fonts.
///     No system font fallback - only bundled fonts are used.
/// </summary>
public static class FontLoader
{
    /// <summary>
    ///     Path to the Pokemon font file (for loading screen and game UI).
    /// </summary>
    public const string PokemonFontPath = "Assets/Fonts/pokemon.ttf";

    /// <summary>
    ///     Path to the debug font file (for console and debug overlays).
    /// </summary>
    public const string DebugFontPath = "Assets/Fonts/0xProtoNerdFontMono-Regular.ttf";

    /// <summary>
    ///     Loads the debug font (0xProtoNerdFontMono) for console and debug UI.
    /// </summary>
    /// <returns>A FontSystem with the debug font, or null if not found.</returns>
    public static FontSystem? LoadFont()
    {
        return LoadDebugFont();
    }

    /// <summary>
    ///     Loads the debug font (0xProtoNerdFontMono) for console and debug UI.
    /// </summary>
    /// <returns>A FontSystem with the debug font, or null if not found.</returns>
    public static FontSystem? LoadDebugFont()
    {
        return LoadFontFromPath(DebugFontPath, "Debug");
    }

    /// <summary>
    ///     Loads the Pokemon font for loading screen and game UI.
    /// </summary>
    /// <returns>A FontSystem with the Pokemon font, or null if not found.</returns>
    public static FontSystem? LoadPokemonFont()
    {
        return LoadFontFromPath(PokemonFontPath, "Pokemon");
    }

    /// <summary>
    ///     Checks if the bundled debug font is available.
    /// </summary>
    public static bool IsBundledFontAvailable()
    {
        return File.Exists(DebugFontPath);
    }

    /// <summary>
    ///     Checks if the Pokemon font is available.
    /// </summary>
    public static bool IsPokemonFontAvailable()
    {
        return File.Exists(PokemonFontPath);
    }

    private static FontSystem? LoadFontFromPath(string fontPath, string fontName)
    {
        if (!File.Exists(fontPath))
        {
            Console.WriteLine($"[FontLoader] {fontName} font not found at: {fontPath}");
            return null;
        }

        try
        {
            byte[] fontData = File.ReadAllBytes(fontPath);
            var fontSystem = new FontSystem();
            fontSystem.AddFont(fontData);
            Console.WriteLine($"[FontLoader] Loaded {fontName} font from: {fontPath}");
            return fontSystem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FontLoader] Failed to load {fontName} font: {ex.Message}");
            return null;
        }
    }
}
