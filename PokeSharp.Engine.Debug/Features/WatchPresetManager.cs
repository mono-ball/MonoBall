using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Features;

/// <summary>
///     Manages saving and loading watch presets.
/// </summary>
public class WatchPresetManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<WatchPresetManager>? _logger;
    private readonly string _presetsDirectory;

    public WatchPresetManager(string presetsDirectory, ILogger<WatchPresetManager>? logger = null)
    {
        _presetsDirectory = presetsDirectory;
        _logger = logger;

        // Create presets directory if it doesn't exist
        if (!Directory.Exists(_presetsDirectory))
        {
            Directory.CreateDirectory(_presetsDirectory);
            _logger?.LogInformation(
                "Created watch presets directory: {Directory}",
                _presetsDirectory
            );
        }
    }

    /// <summary>
    ///     Saves a watch preset to disk.
    /// </summary>
    public bool SavePreset(WatchPreset preset)
    {
        try
        {
            string filename = GetPresetFilename(preset.Name);
            string filePath = Path.Combine(_presetsDirectory, filename);

            string json = JsonSerializer.Serialize(preset, JsonOptions);
            File.WriteAllText(filePath, json);

            _logger?.LogInformation(
                "Saved watch preset: {Name} ({Count} watches)",
                preset.Name,
                preset.Watches.Count
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save watch preset: {Name}", preset.Name);
            return false;
        }
    }

    /// <summary>
    ///     Loads a watch preset from disk.
    /// </summary>
    public WatchPreset? LoadPreset(string name)
    {
        try
        {
            string filename = GetPresetFilename(name);
            string filePath = Path.Combine(_presetsDirectory, filename);

            if (!File.Exists(filePath))
            {
                _logger?.LogWarning("Watch preset not found: {Name}", name);
                return null;
            }

            string json = File.ReadAllText(filePath);
            WatchPreset? preset = JsonSerializer.Deserialize<WatchPreset>(json, JsonOptions);

            if (preset != null)
            {
                _logger?.LogInformation(
                    "Loaded watch preset: {Name} ({Count} watches)",
                    preset.Name,
                    preset.Watches.Count
                );
            }

            return preset;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load watch preset: {Name}", name);
            return null;
        }
    }

    /// <summary>
    ///     Lists all available presets.
    /// </summary>
    public List<(string Name, string Description, int WatchCount, DateTime CreatedAt)> ListPresets()
    {
        var presets = new List<(string, string, int, DateTime)>();

        try
        {
            string[] files = Directory.GetFiles(_presetsDirectory, "*.json");

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    WatchPreset? preset = JsonSerializer.Deserialize<WatchPreset>(
                        json,
                        JsonOptions
                    );

                    if (preset != null)
                    {
                        presets.Add(
                            (
                                preset.Name,
                                preset.Description,
                                preset.Watches.Count,
                                preset.CreatedAt
                            )
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load preset info from: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list watch presets");
        }

        return presets.OrderBy(p => p.Item1).ToList();
    }

    /// <summary>
    ///     Deletes a watch preset.
    /// </summary>
    public bool DeletePreset(string name)
    {
        try
        {
            string filename = GetPresetFilename(name);
            string filePath = Path.Combine(_presetsDirectory, filename);

            if (!File.Exists(filePath))
            {
                _logger?.LogWarning("Watch preset not found: {Name}", name);
                return false;
            }

            File.Delete(filePath);
            _logger?.LogInformation("Deleted watch preset: {Name}", name);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete watch preset: {Name}", name);
            return false;
        }
    }

    /// <summary>
    ///     Checks if a preset exists.
    /// </summary>
    public bool PresetExists(string name)
    {
        string filename = GetPresetFilename(name);
        string filePath = Path.Combine(_presetsDirectory, filename);
        return File.Exists(filePath);
    }

    /// <summary>
    ///     Gets the filename for a preset.
    /// </summary>
    private static string GetPresetFilename(string name)
    {
        // Sanitize the name for use as a filename
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = string.Join(
            "_",
            name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)
        );
        return $"{sanitized}.json";
    }

    /// <summary>
    ///     Creates built-in presets in the presets directory.
    /// </summary>
    public void CreateBuiltInPresets()
    {
        var presets = new List<WatchPreset>
        {
            CreatePerformancePreset(),
            CreateCombatPreset(),
            CreatePlayerStatsPreset(),
            CreateMemoryPreset(),
        };

        foreach (WatchPreset preset in presets)
        {
            if (!PresetExists(preset.Name))
            {
                SavePreset(preset);
            }
        }
    }

    private static WatchPreset CreatePerformancePreset()
    {
        return new WatchPreset
        {
            Name = "performance",
            Description = "Monitor game performance metrics (FPS, frame time, memory)",
            CreatedAt = DateTime.Now,
            UpdateInterval = 500,
            AutoUpdateEnabled = true,
            Watches = new List<WatchPresetEntry>
            {
                new()
                {
                    Name = "fps",
                    Expression = "Game.GetFPS()",
                    Group = "performance",
                    IsPinned = true,
                    Alert = new WatchAlertConfig { Type = "below", Threshold = "55" },
                },
                new()
                {
                    Name = "frame_time",
                    Expression = "Game.GetFrameTime()",
                    Group = "performance",
                    Alert = new WatchAlertConfig { Type = "above", Threshold = "16.67" },
                },
                new()
                {
                    Name = "memory_mb",
                    Expression = "GC.GetTotalMemory(false) / (1024.0 * 1024.0)",
                    Group = "performance",
                },
            },
        };
    }

    private static WatchPreset CreateCombatPreset()
    {
        return new WatchPreset
        {
            Name = "combat",
            Description = "Monitor combat system values (HP, damage, in-battle status)",
            CreatedAt = DateTime.Now,
            UpdateInterval = 200,
            AutoUpdateEnabled = true,
            Watches = new List<WatchPresetEntry>
            {
                new()
                {
                    Name = "in_battle",
                    Expression = "Game.InBattle()",
                    Group = "combat",
                    IsPinned = true,
                },
                new()
                {
                    Name = "player_hp",
                    Expression = "Player.GetHP()",
                    Group = "combat",
                    Condition = "Game.InBattle()",
                    Alert = new WatchAlertConfig { Type = "below", Threshold = "30" },
                },
                new()
                {
                    Name = "enemy_hp",
                    Expression = "Battle.GetEnemyHP()",
                    Group = "combat",
                    Condition = "Game.InBattle()",
                },
                new()
                {
                    Name = "last_damage",
                    Expression = "Battle.GetLastDamage()",
                    Group = "combat",
                    Condition = "Game.InBattle()",
                },
            },
        };
    }

    private static WatchPreset CreatePlayerStatsPreset()
    {
        return new WatchPreset
        {
            Name = "player_stats",
            Description = "Monitor player statistics (position, money, inventory)",
            CreatedAt = DateTime.Now,
            UpdateInterval = 500,
            AutoUpdateEnabled = true,
            Watches = new List<WatchPresetEntry>
            {
                new()
                {
                    Name = "position",
                    Expression = "Player.GetPosition()",
                    Group = "player",
                },
                new()
                {
                    Name = "money",
                    Expression = "Player.GetMoney()",
                    Group = "player",
                    Alert = new WatchAlertConfig { Type = "changes" },
                },
                new()
                {
                    Name = "map",
                    Expression = "Game.GetCurrentMap()",
                    Group = "player",
                },
            },
        };
    }

    private static WatchPreset CreateMemoryPreset()
    {
        return new WatchPreset
        {
            Name = "memory",
            Description = "Monitor memory and garbage collection metrics",
            CreatedAt = DateTime.Now,
            UpdateInterval = 1000,
            AutoUpdateEnabled = true,
            Watches = new List<WatchPresetEntry>
            {
                new()
                {
                    Name = "total_memory",
                    Expression = "GC.GetTotalMemory(false) / (1024.0 * 1024.0)",
                    Group = "memory",
                    IsPinned = true,
                },
                new()
                {
                    Name = "gen0_collections",
                    Expression = "GC.CollectionCount(0)",
                    Group = "memory",
                    Alert = new WatchAlertConfig { Type = "changes" },
                },
                new()
                {
                    Name = "gen1_collections",
                    Expression = "GC.CollectionCount(1)",
                    Group = "memory",
                },
                new()
                {
                    Name = "gen2_collections",
                    Expression = "GC.CollectionCount(2)",
                    Group = "memory",
                },
            },
        };
    }
}
