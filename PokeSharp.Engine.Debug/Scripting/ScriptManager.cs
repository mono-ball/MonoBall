using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Common;

namespace PokeSharp.Engine.Debug.Scripting;

/// <summary>
///     Manages loading and saving of C# script files (.csx) for the debug console.
/// </summary>
public class ScriptManager
{
    private readonly ILogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the ScriptManager.
    /// </summary>
    /// <param name="scriptsDirectory">Directory where scripts are stored (defaults to "Scripts" in game directory)</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public ScriptManager(string? scriptsDirectory = null, ILogger? logger = null)
    {
        ScriptsDirectory =
            scriptsDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
        _logger = logger;

        // Ensure scripts directory exists
        EnsureScriptsDirectoryExists();
    }

    /// <summary>
    ///     Gets the scripts directory path.
    /// </summary>
    public string ScriptsDirectory { get; }

    /// <summary>
    ///     Loads a script file and returns its contents.
    /// </summary>
    /// <param name="filename">Name of the script file (with or without .csx extension)</param>
    /// <returns>Result containing script content on success, or error details on failure</returns>
    public Result<string> LoadScript(string filename)
    {
        try
        {
            Result<string> pathResult = SanitizeAndValidatePath(filename);
            if (!pathResult.IsSuccess)
            {
                return pathResult.Exception != null
                    ? Result<string>.Failure(pathResult.Error!, pathResult.Exception)
                    : Result<string>.Failure(pathResult.Error!);
            }

            string fullPath = pathResult.Value!;

            if (!File.Exists(fullPath))
            {
                _logger?.LogWarning("Script file not found: {Path}", fullPath);
                return Result<string>.Failure($"Script not found: {filename}");
            }

            string content = File.ReadAllText(fullPath);
            _logger?.LogInformation(
                "Loaded script: {Filename} ({Length} chars)",
                filename,
                content.Length
            );
            return Result<string>.Success(content);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load script: {Filename}", filename);
            return Result<string>.Failure($"Failed to load script: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Saves script content to a file.
    /// </summary>
    /// <param name="filename">Name of the script file (with or without .csx extension)</param>
    /// <param name="content">Script content to save</param>
    /// <returns>Result indicating success or failure with error details</returns>
    public Result SaveScript(string filename, string content)
    {
        try
        {
            Result<string> pathResult = SanitizeAndValidatePath(filename);
            if (!pathResult.IsSuccess)
            {
                return pathResult.Exception != null
                    ? Result.Failure(pathResult.Error!, pathResult.Exception)
                    : Result.Failure(pathResult.Error!);
            }

            string fullPath = pathResult.Value!;

            File.WriteAllText(fullPath, content);
            _logger?.LogInformation(
                "Saved script: {Filename} ({Length} chars)",
                filename,
                content.Length
            );
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save script: {Filename}", filename);
            return Result.Failure($"Failed to save script: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Lists all available script files.
    /// </summary>
    /// <returns>List of script filenames (without full path)</returns>
    public List<string> ListScripts()
    {
        try
        {
            if (!Directory.Exists(ScriptsDirectory))
            {
                return new List<string>();
            }

            var files = Directory
                .GetFiles(ScriptsDirectory, "*.csx", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Select(name => name!)
                .OrderBy(name => name)
                .ToList();

            _logger?.LogDebug(
                "Found {Count} scripts in {Directory}",
                files.Count,
                ScriptsDirectory
            );
            return files;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list scripts in: {Directory}", ScriptsDirectory);
            return new List<string>();
        }
    }

    /// <summary>
    ///     Deletes a script file.
    /// </summary>
    /// <param name="filename">Name of the script file to delete</param>
    /// <returns>Result indicating success or failure with error details</returns>
    public Result DeleteScript(string filename)
    {
        try
        {
            Result<string> pathResult = SanitizeAndValidatePath(filename);
            if (!pathResult.IsSuccess)
            {
                return pathResult.Exception != null
                    ? Result.Failure(pathResult.Error!, pathResult.Exception)
                    : Result.Failure(pathResult.Error!);
            }

            string fullPath = pathResult.Value!;

            if (!File.Exists(fullPath))
            {
                _logger?.LogWarning("Script file not found for deletion: {Path}", fullPath);
                return Result.Failure($"Script not found: {filename}");
            }

            File.Delete(fullPath);
            _logger?.LogInformation("Deleted script: {Filename}", filename);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete script: {Filename}", filename);
            return Result.Failure($"Failed to delete script: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Checks if a script file exists.
    /// </summary>
    /// <param name="filename">Name of the script file</param>
    /// <returns>True if the file exists, false otherwise</returns>
    public bool ScriptExists(string filename)
    {
        try
        {
            Result<string> pathResult = SanitizeAndValidatePath(filename);
            if (!pathResult.IsSuccess)
            {
                return false;
            }

            return File.Exists(pathResult.Value!);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if script exists: {Filename}", filename);
            return false;
        }
    }

    /// <summary>
    ///     Sanitizes and validates a script filename, ensuring it's safe and within the scripts directory.
    /// </summary>
    /// <param name="filename">The filename to sanitize and validate.</param>
    /// <returns>Result containing the full validated path on success, or error details on failure.</returns>
    private Result<string> SanitizeAndValidatePath(string filename)
    {
        try
        {
            // Sanitize filename to prevent path traversal attacks
            filename = Path.GetFileName(filename); // Remove any directory components

            if (string.IsNullOrWhiteSpace(filename))
            {
                _logger?.LogError("Invalid script filename: empty or whitespace");
                return Result<string>.Failure("Script filename cannot be empty");
            }

            // Add .csx extension if not present
            if (!filename.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".csx";
            }

            string fullPath = Path.GetFullPath(Path.Combine(ScriptsDirectory, filename));

            // Ensure the resolved path is within the scripts directory (prevent traversal)
            string scriptsDir = Path.GetFullPath(ScriptsDirectory);
            if (!fullPath.StartsWith(scriptsDir, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogError("Path traversal attempt detected: {Filename}", filename);
                return Result<string>.Failure($"Invalid script path: {filename}");
            }

            return Result<string>.Success(fullPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sanitizing path for: {Filename}", filename);
            return Result<string>.Failure($"Failed to validate path: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Ensures the scripts directory exists, creating it if necessary.
    /// </summary>
    private void EnsureScriptsDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(ScriptsDirectory))
            {
                Directory.CreateDirectory(ScriptsDirectory);
                _logger?.LogInformation("Created scripts directory: {Directory}", ScriptsDirectory);

                // Create a default example script
                CreateDefaultExampleScript();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to create scripts directory: {Directory}",
                ScriptsDirectory
            );
        }
    }

    /// <summary>
    ///     Creates a default example script to help users get started.
    /// </summary>
    private void CreateDefaultExampleScript()
    {
        try
        {
            string exampleScript =
                @"// Example Debug Script
// This script demonstrates common debugging tasks using the Scripting API
// Uses ScriptContext pattern (same as NPC behaviors)

Print(""=== Example Script ===="");
Print("""");

// === PLAYER INFO ===
Print(""Getting player info..."");
var playerName = Player.GetPlayerName();
var playerPos = Player.GetPlayerPosition();
var playerMoney = Player.GetMoney();

Print($""Player: {playerName}"");
Print($""Position: ({playerPos.X}, {playerPos.Y})"");
Print($""Money: ${playerMoney}"");
Print("""");

// === WORLD INFO ===
// Use CountEntities() helper (handles QueryDescription correctly)
Print($""Total Entities: {CountEntities()}"");
Print("""");

// === EXAMPLES OF WHAT YOU CAN DO ===
Print(""Example commands (uncomment to use):"");
Print("""");

// Example 1: Give player money
// Player.GiveMoney(1000);
// Print(""Gave player $1000"");

// Example 2: Check if player has enough money
// if (Player.HasMoney(500))
//     Print(""Player has at least $500"");

// Example 3: Lock player movement (useful for cutscenes)
// Player.SetPlayerMovementLocked(true);
// Print(""Player movement locked"");

// Example 4: Teleport to a different location
// Map.TransitionToMap(Map.GetCurrentMapId(), 10, 10);
// Print(""Teleported to (10, 10)"");

// Example 5: Check if a position is walkable
// var walkable = Map.IsPositionWalkable(Map.GetCurrentMapId(), 15, 15);
// Print($""Position (15, 15) walkable: {walkable}"");

// Example 6: Set a game flag
// GameState.SetFlag(""example_flag"", true);
// Print(""Set example flag to true"");

Print(""[+] Example script executed successfully!"");
Print("""");
Print(""TIP: Type 'scripts' to see all available scripts"");
Print(""     Type 'load debug-info' for detailed system info"");
Print(""     Type 'Help()' to see all available methods"");
";

            string examplePath = Path.Combine(ScriptsDirectory, "example.csx");
            File.WriteAllText(examplePath, exampleScript);
            _logger?.LogInformation("Created example script: example.csx");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create example script");
        }
    }
}
