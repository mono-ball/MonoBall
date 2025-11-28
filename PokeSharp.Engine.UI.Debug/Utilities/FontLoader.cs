using System.Reflection;
using FontStashSharp;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
/// Utility for loading fonts for debug UI.
/// Loads the bundled Iosevka Nerd Font first, with system font fallback.
/// </summary>
public static class FontLoader
{
    /// <summary>
    /// The name of the bundled font resource.
    /// </summary>
    private const string BundledFontResourceName = "PokeSharp.Engine.UI.Debug.Assets.Fonts.0xProtoNerdFontMono-Regular.ttf";

    /// <summary>
    /// Alternative resource name patterns to try.
    /// </summary>
    private static readonly string[] AlternativeResourcePatterns =
    {
        "0xProtoNerdFontMono-Regular.ttf",
        "0xProtoNerdFont-Regular.ttf",
        "0xProto"
    };

    /// <summary>
    /// Attempts to load the bundled Iosevka Nerd Font, falling back to system fonts if not found.
    /// </summary>
    /// <returns>A FontSystem with a loaded font, or null if none found.</returns>
    public static FontSystem? LoadFont()
    {
        // Try bundled font first
        var fontSystem = LoadBundledFont();
        if (fontSystem != null)
        {
            return fontSystem;
        }

        // Fall back to system fonts
        return LoadSystemMonospaceFont();
    }

    /// <summary>
    /// Attempts to load the bundled Iosevka Nerd Font from embedded resources.
    /// </summary>
    /// <returns>A FontSystem with the bundled font, or null if not found.</returns>
    public static FontSystem? LoadBundledFont()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Try the primary resource name
        var stream = assembly.GetManifestResourceStream(BundledFontResourceName);

        // If not found, search for any matching resource
        if (stream == null)
        {
            var resourceNames = assembly.GetManifestResourceNames();
            foreach (var pattern in AlternativeResourcePatterns)
            {
                var matchingResource = resourceNames.FirstOrDefault(r =>
                    r.Contains(pattern, StringComparison.OrdinalIgnoreCase));

                if (matchingResource != null)
                {
                    stream = assembly.GetManifestResourceStream(matchingResource);
                    if (stream != null)
                    {
                        break;
                    }
                }
            }
        }

        if (stream == null)
        {
            return null;
        }

        try
        {
            using (stream)
            {
                var fontData = new byte[stream.Length];
                stream.ReadExactly(fontData);

                var fontSystem = new FontSystem();
                fontSystem.AddFont(fontData);
                return fontSystem;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to load a monospace font from system paths.
    /// Used as fallback if bundled font is not available.
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
    /// Gets a list of available embedded font resources.
    /// Useful for debugging font loading issues.
    /// </summary>
    public static string[] GetEmbeddedFontResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceNames()
            .Where(r => r.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        r.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Gets a list of font paths that were checked for system fonts.
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

    /// <summary>
    /// Checks if the bundled font is available.
    /// </summary>
    public static bool IsBundledFontAvailable()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        return resourceNames.Any(r =>
            r.Contains("0xProto", StringComparison.OrdinalIgnoreCase) &&
            (r.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
             r.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)));
    }
}
