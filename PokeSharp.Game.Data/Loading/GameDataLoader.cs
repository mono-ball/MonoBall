using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Data.Entities;

namespace PokeSharp.Game.Data.Loading;

/// <summary>
///     Loads game data from JSON files into EF Core in-memory database.
///     Focuses on NPCs and trainers initially.
/// </summary>
public class GameDataLoader
{
    private readonly GameDataContext _context;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<GameDataLoader> _logger;

    public GameDataLoader(GameDataContext context, ILogger<GameDataLoader> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };
    }

    /// <summary>
    ///     Load all game data from JSON files.
    /// </summary>
    public async Task LoadAllAsync(string dataPath, CancellationToken ct = default)
    {
        _logger.LogGameDataLoadingStarted(dataPath);

        var loadedCounts = new Dictionary<string, int>();

        // Load NPCs
        string npcsPath = Path.Combine(dataPath, "NPCs");
        loadedCounts["NPCs"] = await LoadNpcsAsync(npcsPath, ct);

        // Load Trainers
        string trainersPath = Path.Combine(dataPath, "Trainers");
        loadedCounts["Trainers"] = await LoadTrainersAsync(trainersPath, ct);

        // Load Maps
        string mapsPath = Path.Combine(dataPath, "Maps");
        loadedCounts["Maps"] = await LoadMapsAsync(mapsPath, ct);

        // Log summary
        _logger.LogGameDataLoaded(loadedCounts);
    }

    /// <summary>
    ///     Load NPC definitions from JSON files.
    /// </summary>
    private async Task<int> LoadNpcsAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogDirectoryNotFound("NPC", path);
            return 0;
        }

        string[] files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                NpcDefinitionDto? dto = JsonSerializer.Deserialize<NpcDefinitionDto>(
                    json,
                    _jsonOptions
                );

                if (dto == null)
                {
                    _logger.LogNpcDeserializeFailed(file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.NpcId))
                {
                    _logger.LogNpcMissingField(file, "npcId");
                    continue;
                }

                // Convert DTO to entity
                var npc = new NpcDefinition
                {
                    NpcId = dto.NpcId,
                    DisplayName = dto.DisplayName ?? dto.NpcId,
                    NpcType = dto.NpcType,
                    SpriteId = SpriteId.TryCreate(dto.SpriteId),
                    BehaviorScript = dto.BehaviorScript,
                    DialogueScript = dto.DialogueScript,
                    MovementSpeed = dto.MovementSpeed ?? 2.0f,
                    CustomPropertiesJson =
                        dto.CustomProperties != null
                            ? JsonSerializer.Serialize(dto.CustomProperties, _jsonOptions)
                            : null,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0",
                };

                _context.Npcs.Add(npc);
                count++;

                _logger.LogNpcLoaded(npc.NpcId);
            }
            catch (Exception ex)
            {
                _logger.LogNpcLoadFailed(file, ex);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogNpcsLoaded(count);
        return count;
    }

    /// <summary>
    ///     Load trainer definitions from JSON files.
    /// </summary>
    private async Task<int> LoadTrainersAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogDirectoryNotFound("Trainer", path);
            return 0;
        }

        string[] files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                TrainerDefinitionDto? dto = JsonSerializer.Deserialize<TrainerDefinitionDto>(
                    json,
                    _jsonOptions
                );

                if (dto == null)
                {
                    _logger.LogTrainerDeserializeFailed(file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.TrainerId))
                {
                    _logger.LogTrainerMissingField(file, "trainerId");
                    continue;
                }

                // Convert DTO to entity
                var trainer = new TrainerDefinition
                {
                    TrainerId = dto.TrainerId,
                    DisplayName = dto.DisplayName ?? dto.TrainerId,
                    TrainerClass = dto.TrainerClass ?? "trainer",
                    SpriteId = SpriteId.TryCreate(dto.SpriteId),
                    PrizeMoney = dto.PrizeMoney ?? 100,
                    Items = dto.Items != null ? string.Join(",", dto.Items) : null,
                    AiScript = dto.AiScript,
                    IntroDialogue = dto.IntroDialogue,
                    DefeatDialogue = dto.DefeatDialogue,
                    OnDefeatScript = dto.OnDefeatScript,
                    IsRematchable = dto.IsRematchable ?? false,
                    PartyJson =
                        dto.Party != null
                            ? JsonSerializer.Serialize(dto.Party, _jsonOptions)
                            : "[]",
                    CustomPropertiesJson =
                        dto.CustomProperties != null
                            ? JsonSerializer.Serialize(dto.CustomProperties, _jsonOptions)
                            : null,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0",
                };

                _context.Trainers.Add(trainer);
                count++;

                _logger.LogTrainerLoaded(trainer.TrainerId);
            }
            catch (Exception ex)
            {
                _logger.LogTrainerLoadFailed(file, ex);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogTrainersLoaded(count);
        return count;
    }

    /// <summary>
    ///     Load map definitions from Tiled JSON files.
    ///     Stores relative path to file instead of full JSON data.
    /// </summary>
    private async Task<int> LoadMapsAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogDirectoryNotFound("Maps", path);
            return 0;
        }

        // Determine Assets root for relative path calculation
        string assetsRoot = ResolveAssetsRoot(path);

        string[] files = Directory
            .GetFiles(path, "*.json", SearchOption.AllDirectories)
            .Where(f => !IsHiddenOrSystemDirectory(f)) // Skip hidden directories like .claude-flow
            .ToArray();
        int count = 0;

        // OPTIMIZATION: Load all existing maps once to avoid N+1 queries
        // Using AsNoTracking since we'll manually track changes
        Dictionary<MapIdentifier, MapDefinition> existingMaps = await _context
            .Maps.AsNoTracking()
            .ToDictionaryAsync(m => m.MapId, ct);

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Read Tiled JSON to extract metadata only
                string tiledJson = await File.ReadAllTextAsync(file, ct);

                // Parse to extract metadata (use a lightweight DTO)
                TiledMapMetadataDto? tiledDoc = JsonSerializer.Deserialize<TiledMapMetadataDto>(
                    tiledJson,
                    _jsonOptions
                );

                if (tiledDoc == null)
                {
                    _logger.LogTiledParseFailed(file);
                    continue;
                }

                // Generate map ID from filename
                string mapId = Path.GetFileNameWithoutExtension(file);

                // Extract metadata from Tiled custom properties (convert array to dictionary)
                Dictionary<string, object> properties = ConvertTiledPropertiesToDictionary(
                    tiledDoc.Properties
                );

                // Calculate relative path from Assets root
                string relativePath = Path.GetRelativePath(assetsRoot, file);

                // Parse map connections from structured Connection properties (includes offsets)
                (
                    MapIdentifier? northMapId,
                    int northOffset,
                    MapIdentifier? southMapId,
                    int southOffset,
                    MapIdentifier? eastMapId,
                    int eastOffset,
                    MapIdentifier? westMapId,
                    int westOffset
                ) = ParseMapConnections(properties);

                var mapDef = new MapDefinition
                {
                    MapId = new MapIdentifier(mapId),
                    DisplayName = GetPropertyString(properties, "displayName") ?? mapId,
                    Region = GetPropertyString(properties, "region") ?? "hoenn",
                    MapType = GetPropertyString(properties, "mapType"),
                    TiledDataPath = relativePath, // Store relative path instead of JSON
                    MusicId = GetPropertyString(properties, "music"),
                    Weather = GetPropertyString(properties, "weather") ?? "clear",
                    ShowMapName = GetPropertyBool(properties, "showMapName") ?? true,
                    CanFly = GetPropertyBool(properties, "canFly") ?? false,
                    BackgroundImage = GetPropertyString(properties, "backgroundImage"),
                    NorthMapId = northMapId,
                    NorthConnectionOffset = northOffset,
                    SouthMapId = southMapId,
                    SouthConnectionOffset = southOffset,
                    EastMapId = eastMapId,
                    EastConnectionOffset = eastOffset,
                    WestMapId = westMapId,
                    WestConnectionOffset = westOffset,
                    EncounterDataJson = GetPropertyString(properties, "encounters"),
                    SourceMod = GetPropertyString(properties, "sourceMod"),
                    Version = GetPropertyString(properties, "version") ?? "1.0.0",
                };

                // Support mod overrides: Check in-memory dictionary instead of querying DB
                if (existingMaps.TryGetValue(mapId, out MapDefinition? existing))
                {
                    // Mod is overriding base game map - attach and update
                    _context.Maps.Attach(existing);
                    _context.Entry(existing).CurrentValues.SetValues(mapDef);
                    _logger.LogMapOverridden(mapDef.MapId, mapDef.DisplayName);
                }
                else
                {
                    _context.Maps.Add(mapDef);
                }

                count++;

                _logger.LogMapLoadedFromFile(mapDef.MapId, mapDef.DisplayName, relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogMapLoadFailed(file, ex);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogMapsLoaded(count);
        return count;
    }

    /// <summary>
    ///     Resolves the Assets root directory for relative path calculation.
    /// </summary>
    private string ResolveAssetsRoot(string mapsPath)
    {
        // Walk up from Maps path to find Assets directory
        var current = new DirectoryInfo(mapsPath);
        while (current != null)
        {
            if (current.Name.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        // Fallback: assume Assets is 2 levels up from Maps (Assets/Data/Maps)
        return Path.GetFullPath(Path.Combine(mapsPath, "..", ".."));
    }

    // Helper methods for extracting properties from Tiled custom properties

    /// <summary>
    ///     Converts Tiled's properties array format into a dictionary for easier access.
    ///     Tiled format: [{ "name": "key", "type": "string", "value": "val" }, ...]
    /// </summary>
    private static Dictionary<string, object> ConvertTiledPropertiesToDictionary(
        List<TiledPropertyDto>? properties
    )
    {
        var dict = new Dictionary<string, object>();

        if (properties == null)
        {
            return dict;
        }

        foreach (TiledPropertyDto prop in properties)
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
        if (props.TryGetValue(key, out object? value))
        {
            return value?.ToString();
        }

        return null;
    }

    private static bool? GetPropertyBool(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out object? value))
        {
            if (value is bool b)
            {
                return b;
            }

            if (value is JsonElement je && je.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value is JsonElement je2 && je2.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (bool.TryParse(value?.ToString(), out bool result))
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    ///     Parses map connections from structured Connection class properties.
    ///     Looks for properties named connection_north, connection_south, etc.
    ///     and extracts both the "map" field and "offset" field from the value object.
    /// </summary>
    private static (
        MapIdentifier? North,
        int NorthOffset,
        MapIdentifier? South,
        int SouthOffset,
        MapIdentifier? East,
        int EastOffset,
        MapIdentifier? West,
        int WestOffset
    ) ParseMapConnections(Dictionary<string, object> properties)
    {
        MapIdentifier? north = null,
            south = null,
            east = null,
            west = null;
        int northOffset = 0,
            southOffset = 0,
            eastOffset = 0,
            westOffset = 0;

        // Check for connection_north
        if (properties.TryGetValue("connection_north", out object? northValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(northValue);
            north = MapIdentifier.TryCreate(mapId);
            northOffset = offset;
        }

        // Check for connection_south
        if (properties.TryGetValue("connection_south", out object? southValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(southValue);
            south = MapIdentifier.TryCreate(mapId);
            southOffset = offset;
        }

        // Check for connection_east
        if (properties.TryGetValue("connection_east", out object? eastValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(eastValue);
            east = MapIdentifier.TryCreate(mapId);
            eastOffset = offset;
        }

        // Check for connection_west
        if (properties.TryGetValue("connection_west", out object? westValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(westValue);
            west = MapIdentifier.TryCreate(mapId);
            westOffset = offset;
        }

        return (north, northOffset, south, southOffset, east, eastOffset, west, westOffset);
    }

    /// <summary>
    ///     Extracts both "map" and "offset" fields from a Connection property value.
    ///     Handles both JsonElement and Dictionary formats.
    /// </summary>
    /// <returns>A tuple of (mapId, offset) where offset defaults to 0 if not present.</returns>
    private static (string? MapId, int Offset) ExtractConnectionData(object? connectionValue)
    {
        if (connectionValue == null)
        {
            return (null, 0);
        }

        try
        {
            // Handle JsonElement case
            if (
                connectionValue is JsonElement jsonElement
                && jsonElement.ValueKind == JsonValueKind.Object
            )
            {
                string? mapId = null;
                int offset = 0;

                if (jsonElement.TryGetProperty("map", out JsonElement mapProp))
                {
                    mapId = mapProp.GetString();
                }

                if (jsonElement.TryGetProperty("offset", out JsonElement offsetProp))
                {
                    if (offsetProp.ValueKind == JsonValueKind.Number)
                    {
                        offset = offsetProp.GetInt32();
                    }
                }

                return (mapId, offset);
            }
            // Handle Dictionary case

            if (connectionValue is Dictionary<string, object> dict)
            {
                string? mapId = null;
                int offset = 0;

                if (dict.TryGetValue("map", out object? mapValue))
                {
                    mapId = mapValue?.ToString();
                }

                if (dict.TryGetValue("offset", out object? offsetValue))
                {
                    if (offsetValue is int intOffset)
                    {
                        offset = intOffset;
                    }
                    else if (offsetValue is JsonElement je && je.ValueKind == JsonValueKind.Number)
                    {
                        offset = je.GetInt32();
                    }
                    else if (int.TryParse(offsetValue?.ToString(), out int parsedOffset))
                    {
                        offset = parsedOffset;
                    }
                }

                return (mapId, offset);
            }
        }
        catch
        {
            return (null, 0);
        }

        return (null, 0);
    }

    /// <summary>
    ///     Checks if a file path contains hidden or system directories (e.g., .claude-flow, .git).
    /// </summary>
    private static bool IsHiddenOrSystemDirectory(string filePath)
    {
        string[] pathParts = filePath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        return pathParts.Any(part => part.StartsWith("."));
    }
}

#region DTOs for JSON Deserialization

/// <summary>
///     DTO for deserializing NPC JSON files.
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
///     DTO for deserializing Trainer JSON files.
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
///     Lightweight DTO for extracting metadata from Tiled JSON.
///     Only parses the "properties" field; full JSON is stored as-is.
/// </summary>
internal record TiledMapMetadataDto
{
    public List<TiledPropertyDto>? Properties { get; init; }
}

/// <summary>
///     DTO for Tiled custom property format.
///     Tiled stores properties as: { "name": "key", "type": "string", "value": "val" }
/// </summary>
internal record TiledPropertyDto
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public object? Value { get; init; }
}

#endregion
