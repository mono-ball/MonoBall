using System;
using System.IO;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
/// Handles loading and execution of console startup scripts.
/// </summary>
public static class StartupScriptLoader
{
    private static readonly string StartupScriptPath;

    static StartupScriptLoader()
    {
        // Save to user's local app data directory (same as history)
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pokeSharpDataPath = Path.Combine(appDataPath, "PokeSharp");

        // Create directory if it doesn't exist
        Directory.CreateDirectory(pokeSharpDataPath);

        StartupScriptPath = Path.Combine(pokeSharpDataPath, "startup.csx");
    }

    /// <summary>
    /// Checks if a startup script exists.
    /// </summary>
    public static bool StartupScriptExists()
    {
        return File.Exists(StartupScriptPath);
    }

    /// <summary>
    /// Loads the startup script content if it exists.
    /// </summary>
    /// <returns>The script content, or null if the file doesn't exist or can't be read.</returns>
    public static string? LoadStartupScript()
    {
        try
        {
            if (!File.Exists(StartupScriptPath))
                return null;

            return File.ReadAllText(StartupScriptPath);
        }
        catch (Exception)
        {
            // Silently fail - don't crash the console over a startup script
            return null;
        }
    }

    /// <summary>
    /// Saves startup script content to disk.
    /// </summary>
    /// <param name="content">The script content to save.</param>
    public static bool SaveStartupScript(string content)
    {
        try
        {
            File.WriteAllText(StartupScriptPath, content);
            return true;
        }
        catch (Exception)
        {
            // Silently fail
            return false;
        }
    }

    /// <summary>
    /// Creates a default startup script with helpful examples.
    /// </summary>
    public static void CreateDefaultStartupScript()
    {
        var defaultScript = @"// PokeSharp Console Startup Script
// This script runs automatically when the console opens.
// You can define helper functions, variables, or run initialization code here.

// Example: Define a helper function
void Hello(string name)
{
    Print($""Hello, {name}!"");
}

// Example: Set up common variables
// var myVar = 42;

// Example: Print a welcome message
Print(""Startup script loaded successfully!"");
Print(""Type 'Hello(""""World"""")' to test the helper function."");
";

        SaveStartupScript(defaultScript);
    }

    /// <summary>
    /// Deletes the startup script if it exists.
    /// </summary>
    public static bool DeleteStartupScript()
    {
        try
        {
            if (File.Exists(StartupScriptPath))
            {
                File.Delete(StartupScriptPath);
            }
            return true;
        }
        catch (Exception)
        {
            // Silently fail
            return false;
        }
    }

    /// <summary>
    /// Gets the path to the startup script file for debugging purposes.
    /// </summary>
    public static string GetStartupScriptPath() => StartupScriptPath;
}




