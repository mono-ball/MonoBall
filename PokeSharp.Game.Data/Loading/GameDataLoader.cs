using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Data.Entities;

namespace PokeSharp.Game.Data.Loading;

/// <summary>
/// Loads game data from JSON files into EF Core in-memory database.
/// Focuses on NPCs and trainers initially.
/// </summary>
public class GameDataLoader
{
    private readonly GameDataContext _context;
    private readonly ILogger<GameDataLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameDataLoader(
        GameDataContext context,
        ILogger<GameDataLoader> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Load all game data from JSON files.
    /// </summary>
    public async Task LoadAllAsync(string dataPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Loading game data from {Path}", dataPath);

        var loadedCounts = new Dictionary<string, int>();

        // Load NPCs
        var npcsPath = Path.Combine(dataPath, "NPCs");
        loadedCounts["NPCs"] = await LoadNpcsAsync(npcsPath, ct);

        // Load Trainers
        var trainersPath = Path.Combine(dataPath, "Trainers");
        loadedCounts["Trainers"] = await LoadTrainersAsync(trainersPath, ct);

        // Load Maps
        var mapsPath = Path.Combine(dataPath, "Maps");
        loadedCounts["Maps"] = await LoadMapsAsync(mapsPath, ct);

        // Log summary
        _logger.LogInformation("Game data loaded: {Summary}",
            string.Join(", ", loadedCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
    }

    /// <summary>
    /// Load NPC definitions from JSON files.
    /// </summary>
    private async Task<int> LoadNpcsAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("NPC directory not found: {Path}", path);
            return 0;
        }

        var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        var count = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var dto = JsonSerializer.Deserialize<NpcDefinitionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize NPC from {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.NpcId))
                {
                    _logger.LogWarning("NPC in {File} missing npcId", file);
                    continue;
                }

                // Convert DTO to entity
                var npc = new NpcDefinition
                {
                    NpcId = dto.NpcId,
                    DisplayName = dto.DisplayName ?? dto.NpcId,
                    NpcType = dto.NpcType,
                    SpriteId = dto.SpriteId,
                    BehaviorScript = dto.BehaviorScript,
                    DialogueScript = dto.DialogueScript,
                    MovementSpeed = dto.MovementSpeed ?? 2.0f,
                    CustomPropertiesJson = dto.CustomProperties != null
                        ? JsonSerializer.Serialize(dto.CustomProperties, _jsonOptions)
                        : null,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.Npcs.Add(npc);
                count++;

                _logger.LogDebug("Loaded NPC: {NpcId}", npc.NpcId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading NPC from {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} NPCs", count);
        return count;
    }

    /// <summary>
    /// Load trainer definitions from JSON files.
    /// </summary>
    private async Task<int> LoadTrainersAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Trainer directory not found: {Path}", path);
            return 0;
        }

        var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        var count = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var dto = JsonSerializer.Deserialize<TrainerDefinitionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize Trainer from {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.TrainerId))
                {
                    _logger.LogWarning("Trainer in {File} missing trainerId", file);
                    continue;
                }

                // Convert DTO to entity
                var trainer = new TrainerDefinition
                {
                    TrainerId = dto.TrainerId,
                    DisplayName = dto.DisplayName ?? dto.TrainerId,
                    TrainerClass = dto.TrainerClass ?? "trainer",
                    SpriteId = dto.SpriteId,
                    PrizeMoney = dto.PrizeMoney ?? 100,
                    Items = dto.Items != null ? string.Join(",", dto.Items) : null,
                    AiScript = dto.AiScript,
                    IntroDialogue = dto.IntroDialogue,
                    DefeatDialogue = dto.DefeatDialogue,
                    OnDefeatScript = dto.OnDefeatScript,
                    IsRematchable = dto.IsRematchable ?? false,
                    PartyJson = dto.Party != null
                        ? JsonSerializer.Serialize(dto.Party, _jsonOptions)
                        : "[]",
                    CustomPropertiesJson = dto.CustomProperties != null
                        ? JsonSerializer.Serialize(dto.CustomProperties, _jsonOptions)
                        : null,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.Trainers.Add(trainer);
                count++;

                _logger.LogDebug("Loaded Trainer: {TrainerId}", trainer.TrainerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Trainer from {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} trainers", count);
        return count;
    }

    /// <summary>
    /// Load map definitions from Tiled JSON files.
    /// Stores complete Tiled JSON data in TiledDataJson field.
    /// </summary>
    private async Task<int> LoadMapsAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Maps directory not found: {Path}", path);
            return 0;
        }

        var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        var count = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Read raw Tiled JSON
                var tiledJson = await File.ReadAllTextAsync(file, ct);

                // Parse to extract metadata (use a lightweight DTO)
                var tiledDoc = JsonSerializer.Deserialize<TiledMapMetadataDto>(tiledJson, _jsonOptions);

                if (tiledDoc == null)
                {
                    _logger.LogWarning("Failed to parse Tiled JSON from {File}", file);
                    continue;
                }

                // Generate map ID from filename
                var mapId = Path.GetFileNameWithoutExtension(file);

                // Extract metadata from Tiled custom properties (convert array to dictionary)
                var properties = ConvertTiledPropertiesToDictionary(tiledDoc.Properties);

                var mapDef = new MapDefinition
                {
                    MapId = mapId,
                    DisplayName = GetPropertyString(properties, "displayName") ?? mapId,
                    Region = GetPropertyString(properties, "region") ?? "hoenn",
                    MapType = GetPropertyString(properties, "mapType"),
                    TiledDataJson = tiledJson, // Store complete Tiled JSON
                    MusicId = GetPropertyString(properties, "music"),
                    Weather = GetPropertyString(properties, "weather") ?? "clear",
                    ShowMapName = GetPropertyBool(properties, "showMapName") ?? true,
                    CanFly = GetPropertyBool(properties, "canFly") ?? false,
                    BackgroundImage = GetPropertyString(properties, "backgroundImage"),
                    NorthMapId = GetPropertyString(properties, "northMap"),
                    SouthMapId = GetPropertyString(properties, "southMap"),
                    EastMapId = GetPropertyString(properties, "eastMap"),
                    WestMapId = GetPropertyString(properties, "westMap"),
                    EncounterDataJson = GetPropertyString(properties, "encounters"),
                    SourceMod = GetPropertyString(properties, "sourceMod"),
                    Version = GetPropertyString(properties, "version") ?? "1.0.0"
                };

                _context.Maps.Add(mapDef);
                count++;

                _logger.LogDebug("Loaded Map: {MapId} ({DisplayName})", mapDef.MapId, mapDef.DisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Map from {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} maps", count);
        return count;
    }

    // Helper methods for extracting properties from Tiled custom properties

    /// <summary>
    /// Converts Tiled's properties array format into a dictionary for easier access.
    /// Tiled format: [{ "name": "key", "type": "string", "value": "val" }, ...]
    /// </summary>
    private static Dictionary<string, object> ConvertTiledPropertiesToDictionary(
        List<TiledPropertyDto>? properties)
    {
        var dict = new Dictionary<string, object>();

        if (properties == null)
            return dict;

        foreach (var prop in properties)
        {
            if (!string.IsNullOrEmpty(prop.Name) && prop.Value != null)
            {
                dict[prop.Name] = prop.Value;
            }
        }

        return dict;
    }

    private static string? GetPropertyString(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    private static bool? GetPropertyBool(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            if (value is bool b) return b;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.True) return true;
            if (value is JsonElement je2 && je2.ValueKind == JsonValueKind.False) return false;
            if (bool.TryParse(value?.ToString(), out var result)) return result;
        }
        return null;
    }
}

#region DTOs for JSON Deserialization

/// <summary>
/// DTO for deserializing NPC JSON files.
/// </summary>
internal record NpcDefinitionDto
{
    public string? NpcId { get; init; }
    public string? DisplayName { get; init; }
    public string? NpcType { get; init; }
    public string? SpriteId { get; init; }
    public string? BehaviorScript { get; init; }
    public string? DialogueScript { get; init; }
    public float? MovementSpeed { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
/// DTO for deserializing Trainer JSON files.
/// </summary>
internal record TrainerDefinitionDto
{
    public string? TrainerId { get; init; }
    public string? DisplayName { get; init; }
    public string? TrainerClass { get; init; }
    public string? SpriteId { get; init; }
    public int? PrizeMoney { get; init; }
    public string[]? Items { get; init; }
    public string? AiScript { get; init; }
    public string? IntroDialogue { get; init; }
    public string? DefeatDialogue { get; init; }
    public string? OnDefeatScript { get; init; }
    public bool? IsRematchable { get; init; }
    public TrainerPartyMemberDto[]? Party { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
/// Lightweight DTO for extracting metadata from Tiled JSON.
/// Only parses the "properties" field; full JSON is stored as-is.
/// </summary>
internal record TiledMapMetadataDto
{
    public List<TiledPropertyDto>? Properties { get; init; }
}

/// <summary>
/// DTO for Tiled custom property format.
/// Tiled stores properties as: { "name": "key", "type": "string", "value": "val" }
/// </summary>
internal record TiledPropertyDto
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public object? Value { get; init; }
}

#endregion

