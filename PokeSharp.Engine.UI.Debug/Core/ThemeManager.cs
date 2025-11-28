using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
/// Manages UI themes and allows switching between them at runtime.
/// Supports persistence - theme preference is saved and loaded automatically.
/// </summary>
public static class ThemeManager
{
    // Use lazy initialization to avoid circular dependency issues
    private static UITheme? _currentTheme;
    private static readonly Dictionary<string, UITheme> _themes = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    // Persistence
    private const string AppDataFolderName = "PokeSharp";
    private const string ThemePreferenceFileName = "theme_preference.json";
    private static string _defaultTheme = "pokeball";

    /// <summary>
    /// Sets the default theme to use when no user preference is saved.
    /// Must be called before any theme access to take effect.
    /// </summary>
    public static void SetDefaultTheme(string themeName)
    {
        if (!_initialized)
        {
            _defaultTheme = themeName;
        }
    }

    /// <summary>
    /// Event fired when the theme changes.
    /// </summary>
    public static event Action<UITheme>? ThemeChanged;

    /// <summary>
    /// Gets the current active theme. Returns OneDark as default.
    /// </summary>
    public static UITheme Current
    {
        get
        {
            EnsureInitialized();
            return _currentTheme ?? UITheme.OneDark;
        }
    }

    /// <summary>
    /// Gets all available theme names.
    /// </summary>
    public static IEnumerable<string> AvailableThemes
    {
        get
        {
            EnsureInitialized();
            return _themes.Keys;
        }
    }

    /// <summary>
    /// Ensures themes are registered (lazy initialization).
    /// </summary>
    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // Register all built-in themes
        _themes["onedark"] = UITheme.OneDark;
        _themes["monokai"] = UITheme.Monokai;
        _themes["dracula"] = UITheme.Dracula;
        _themes["gruvbox"] = UITheme.GruvboxDark;
        _themes["nord"] = UITheme.Nord;
        _themes["solarized"] = UITheme.SolarizedDark;
        _themes["solarized-light"] = UITheme.SolarizedLight;
        _themes["pokeball"] = UITheme.Pokeball;

        // Load saved theme preference, or use default from config
        var savedTheme = LoadThemePreference();
        if (savedTheme != null && _themes.TryGetValue(savedTheme, out var theme))
        {
            _currentTheme = theme;
        }
        else if (_themes.TryGetValue(_defaultTheme, out var defaultTheme))
        {
            _currentTheme = defaultTheme;
        }
        else
        {
            // Ultimate fallback - Pokéball for PokeSharp!
            _currentTheme = UITheme.Pokeball;
        }
    }

    /// <summary>
    /// Registers a theme with the given name.
    /// </summary>
    public static void Register(string name, UITheme theme)
    {
        EnsureInitialized();
        _themes[name] = theme;
    }

    /// <summary>
    /// Switches to the theme with the given name.
    /// Saves the preference to disk for persistence.
    /// </summary>
    /// <returns>True if the theme was found and switched, false otherwise.</returns>
    public static bool SetTheme(string name, bool persist = true)
    {
        EnsureInitialized();
        if (_themes.TryGetValue(name, out var theme))
        {
            _currentTheme = theme;
            ThemeChanged?.Invoke(theme);

            // Save preference to disk
            if (persist)
            {
                SaveThemePreference(name);
            }

            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a theme by name, or null if not found.
    /// </summary>
    public static UITheme? GetTheme(string name)
    {
        EnsureInitialized();
        return _themes.TryGetValue(name, out var theme) ? theme : null;
    }

    /// <summary>
    /// Gets the name of the current theme.
    /// </summary>
    public static string GetCurrentThemeName()
    {
        EnsureInitialized();
        foreach (var kvp in _themes)
        {
            if (ReferenceEquals(kvp.Value, _currentTheme))
                return kvp.Key;
        }
        return "unknown";
    }

    /// <summary>
    /// Gets the path to the theme preference file.
    /// </summary>
    private static string GetPreferenceFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pokeSharpPath = Path.Combine(appDataPath, AppDataFolderName);
        return Path.Combine(pokeSharpPath, ThemePreferenceFileName);
    }

    /// <summary>
    /// Saves the theme preference to disk.
    /// </summary>
    private static void SaveThemePreference(string themeName)
    {
        try
        {
            var filePath = GetPreferenceFilePath();
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var preference = new ThemePreference { ThemeName = themeName };
            var json = JsonSerializer.Serialize(preference, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Silently fail - theme persistence is not critical
        }
    }

    /// <summary>
    /// Loads the saved theme preference from disk.
    /// </summary>
    /// <returns>The saved theme name, or null if not found.</returns>
    private static string? LoadThemePreference()
    {
        try
        {
            var filePath = GetPreferenceFilePath();

            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            var preference = JsonSerializer.Deserialize<ThemePreference>(json);
            return preference?.ThemeName;
        }
        catch
        {
            // Silently fail - return null to use default
            return null;
        }
    }

    /// <summary>
    /// Internal class for JSON serialization of theme preference.
    /// </summary>
    private class ThemePreference
    {
        public string? ThemeName { get; set; }
    }
}

