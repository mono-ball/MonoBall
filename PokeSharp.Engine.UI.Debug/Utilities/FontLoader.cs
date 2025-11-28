using FontStashSharp;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
/// Utility for loading system fonts for debug UI.
/// Tries multiple common font paths across different operating systems.
/// </summary>
public static class FontLoader
{
    /// <summary>
    /// Attempts to load a monospace font from system paths.
    /// </summary>
    /// <returns>A FontSystem with a loaded font, or null if none found.</returns>
    public static FontSystem? LoadSystemMonospaceFont()
    {
        var fontSystem = new FontSystem();

        // Try to load common monospace fonts
        string[] fontPaths = new[]
        {
            // macOS
            "/System/Library/Fonts/Monaco.ttf",
            "/System/Library/Fonts/Menlo.ttc",
            "/System/Library/Fonts/Courier New.ttf",
            "/System/Library/Fonts/Supplemental/Courier New.ttf",

            // Windows
            "C:\\Windows\\Fonts\\consola.ttf",
            "C:\\Windows\\Fonts\\cour.ttf",

            // Linux
            "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
            "/usr/share/fonts/TTF/DejaVuSansMono.ttf"
        };

        foreach (var path in fontPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    fontSystem.AddFont(File.ReadAllBytes(path));
                    return fontSystem;
                }
                catch
                {
                    // Try next font
                    continue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a list of font paths that were checked.
    /// </summary>
    public static string[] GetCheckedFontPaths()
    {
        return new[]
        {
            "/System/Library/Fonts/Monaco.ttf",
            "/System/Library/Fonts/Menlo.ttc",
            "/System/Library/Fonts/Courier New.ttf",
            "C:\\Windows\\Fonts\\consola.ttf",
            "C:\\Windows\\Fonts\\cour.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
        };
    }
}




