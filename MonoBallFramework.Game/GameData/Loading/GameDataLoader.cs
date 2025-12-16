using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Loading;

/// <summary>
///     Loads game data from JSON files into EF Core in-memory database.
///     Focuses on NPCs and trainers initially.
/// </summary>
public class GameDataLoader
{
    private readonly GameDataContext _context;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<GameDataLoader> _logger;
    private readonly IContentProvider? _contentProvider;

    public GameDataLoader(
        GameDataContext context,
        ILogger<GameDataLoader> logger,
        IContentProvider? contentProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentProvider = contentProvider;

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

        // Load Maps (from Regions subdirectory)
        string mapsPath = Path.Combine(dataPath, "Maps", "Regions");
        loadedCounts["Maps"] = await LoadMapEntitysAsync(mapsPath, ct);

        // Load Popup Themes
        string themesPath = Path.Combine(dataPath, "Maps", "Popups", "Themes");
        loadedCounts["PopupThemes"] = await LoadPopupThemesAsync(themesPath, ct);

        // Load Map Sections
        string sectionsPath = Path.Combine(dataPath, "Maps", "Sections");
        loadedCounts["MapSections"] = await LoadMapSectionsAsync(sectionsPath, ct);

        // Load Audio Definitions
        string audioPath = Path.Combine(dataPath, "Audio");
        loadedCounts["Audios"] = await LoadAudioEntitysAsync(audioPath, ct);

        // === NEW: Load unified definition types ===

        // Load Sprite Definitions (replaces SpriteRegistry JSON loading)
        string spritesPath = Path.Combine(dataPath, "Sprites");
        loadedCounts["Sprites"] = await LoadSpriteDefinitionsAsync(spritesPath, ct);

        // Load Popup Backgrounds (replaces PopupRegistry JSON loading)
        string backgroundsPath = Path.Combine(dataPath, "Maps", "Popups", "Backgrounds");
        loadedCounts["PopupBackgrounds"] = await LoadPopupBackgroundsAsync(backgroundsPath, ct);

        // Load Popup Outlines (replaces PopupRegistry JSON loading)
        string outlinesPath = Path.Combine(dataPath, "Maps", "Popups", "Outlines");
        loadedCounts["PopupOutlines"] = await LoadPopupOutlinesAsync(outlinesPath, ct);

        // Load Behavior Definitions (replaces TypeRegistry<BehaviorDefinition>)
        string behaviorsPath = Path.Combine(dataPath, "Behaviors");
        loadedCounts["Behaviors"] = await LoadBehaviorDefinitionsAsync(behaviorsPath, ct);

        // Load Tile Behavior Definitions (replaces TypeRegistry<TileBehaviorDefinition>)
        string tileBehaviorsPath = Path.Combine(dataPath, "TileBehaviors");
        loadedCounts["TileBehaviors"] = await LoadTileBehaviorDefinitionsAsync(tileBehaviorsPath, ct);

        // Load Font Definitions (replaces FontLoader hardcoded constants)
        string fontsPath = Path.Combine(dataPath, "Fonts");
        loadedCounts["Fonts"] = await LoadFontDefinitionsAsync(fontsPath, ct);

        // Log summary
        _logger.LogGameDataLoaded(loadedCounts);
    }

    /// <summary>
    ///     Load map definitions from JSON files.
    ///     Simple schema: Id, DisplayName, Type, Region, Description, TiledPath.
    ///     Gameplay metadata (music, weather, connections) is read from Tiled at runtime.
    /// </summary>
    private async Task<int> LoadMapEntitysAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("MapDefinitions", "*.json");
            _logger.LogDebug("Using ContentProvider for MapDefinitions - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("Maps", path);
                return 0;
            }
            files = Directory
                .GetFiles(path, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }
        int count = 0;

        // OPTIMIZATION: Load all existing maps once to avoid N+1 queries
        Dictionary<GameMapId, MapEntity> existingMaps = await _context
            .Maps.AsNoTracking()
            .ToDictionaryAsync(m => m.MapId, ct);

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                MapEntityDto? dto = JsonSerializer.Deserialize<MapEntityDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogMapDefinitionLoadFailed(file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TiledPath))
                {
                    _logger.LogMapDefinitionLoadFailed(file);
                    continue;
                }

                // Parse GameMapId from the Id field
                GameMapId? gameMapId = GameMapId.TryCreate(dto.Id);
                if (gameMapId == null)
                {
                    _logger.LogMapDefinitionLoadFailed(file);
                    continue;
                }

                // Convert DTO to entity - only core fields from definition
                var mapDef = new MapEntity
                {
                    MapId = gameMapId,
                    DisplayName = dto.DisplayName ?? gameMapId.Name,
                    Region = dto.Region ?? "hoenn",
                    MapType = dto.Type,
                    TiledDataPath = dto.TiledPath,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                // Support mod overrides
                if (existingMaps.TryGetValue(gameMapId, out MapEntity? existing))
                {
                    _context.Maps.Attach(existing);
                    _context.Entry(existing).CurrentValues.SetValues(mapDef);
                    _logger.LogMapOverridden(mapDef.MapId, mapDef.DisplayName);
                }
                else
                {
                    _context.Maps.Add(mapDef);
                }

                count++;
                _logger.LogMapDefinitionLoaded(mapDef.MapId.Value, mapDef.TiledDataPath);
            }
            catch (Exception ex)
            {
                _logger.LogMapLoadFailed(file, ex);
            }
        }

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

        // Fallback: assume Assets is 2 levels up from Maps (Assets/Definitions/Maps)
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
    ///     Supports both unified format (base:map:hoenn/name) and legacy format (name).
    /// </summary>
    private static (
        GameMapId? North,
        int NorthOffset,
        GameMapId? South,
        int SouthOffset,
        GameMapId? East,
        int EastOffset,
        GameMapId? West,
        int WestOffset
    ) ParseMapConnections(Dictionary<string, object> properties)
    {
        GameMapId? north = null,
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
            north = GameMapId.TryCreate(mapId);
            northOffset = offset;
        }

        // Check for connection_south
        if (properties.TryGetValue("connection_south", out object? southValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(southValue);
            south = GameMapId.TryCreate(mapId);
            southOffset = offset;
        }

        // Check for connection_east
        if (properties.TryGetValue("connection_east", out object? eastValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(eastValue);
            east = GameMapId.TryCreate(mapId);
            eastOffset = offset;
        }

        // Check for connection_west
        if (properties.TryGetValue("connection_west", out object? westValue))
        {
            (string? mapId, int offset) = ExtractConnectionData(westValue);
            west = GameMapId.TryCreate(mapId);
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

    /// <summary>
    ///     Load popup theme definitions from JSON files.
    /// </summary>
    private async Task<int> LoadPopupThemesAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("PopupThemes", "*.json");
            _logger.LogDebug("Using ContentProvider for PopupThemes - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("PopupThemes", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase));
        }
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                PopupThemeDto? dto = JsonSerializer.Deserialize<PopupThemeDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogPopupThemeLoadFailed(file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    _logger.LogPopupThemeLoadFailed(file);
                    continue;
                }

                // Convert DTO to entity
                // Use TryCreate first for full ID format, fall back to Create for simple names
                var theme = new PopupTheme
                {
                    Id = GameThemeId.TryCreate(dto.Id) ?? GameThemeId.Create(dto.Id),
                    Name = dto.Name ?? dto.Id,
                    Description = dto.Description,
                    Background = dto.Background ?? dto.Id,
                    Outline = dto.Outline ?? $"{dto.Id}_outline",
                    UsageCount = dto.UsageCount ?? 0,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.PopupThemes.Add(theme);
                count++;

                _logger.LogPopupThemeLoaded(theme.Id, theme.Name);
            }
            catch (Exception ex)
            {
                _logger.LogPopupThemeLoadFailed(file, ex);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogPopupThemesLoaded(count);
        return count;
    }

    /// <summary>
    ///     Load map section definitions from JSON files.
    /// </summary>
    private async Task<int> LoadMapSectionsAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("MapSections", "*.json");
            _logger.LogDebug("Using ContentProvider for MapSections - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("MapSections", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase));
        }
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                MapSectionDto? dto = JsonSerializer.Deserialize<MapSectionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogMapSectionLoadFailed(file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Theme))
                {
                    _logger.LogMapSectionLoadFailed(file);
                    continue;
                }

                // Convert DTO to entity
                // Use TryCreate first for full ID format, fall back to Create for simple names
                var section = new MapSection
                {
                    Id = GameMapSectionId.TryCreate(dto.Id) ?? GameMapSectionId.Create(dto.Id),
                    Name = dto.Name ?? dto.Id,
                    ThemeId = GameThemeId.TryCreate(dto.Theme) ?? GameThemeId.Create(dto.Theme),
                    X = dto.X,
                    Y = dto.Y,
                    Width = dto.Width,
                    Height = dto.Height,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.MapSections.Add(section);
                count++;

                _logger.LogMapSectionLoaded(section.Id, section.Name);
            }
            catch (Exception ex)
            {
                _logger.LogMapSectionLoadFailed(file, ex);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogMapSectionsLoaded(count);
        return count;
    }

    /// <summary>
    ///     Load audio definitions from JSON files.
    ///     Recursively processes all subdirectories (Music/Battle, Music/Towns, SFX/Battle, etc.).
    /// </summary>
    private async Task<int> LoadAudioEntitysAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("AudioDefinitions", "*.json");
            _logger.LogDebug("Using ContentProvider for AudioDefinitions - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("AudioEntitys", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                AudioEntityDto? dto = JsonSerializer.Deserialize<AudioEntityDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize audio definition: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.AudioPath))
                {
                    _logger.LogWarning("Audio definition missing required fields: {File}", file);
                    continue;
                }

                // Parse GameAudioId from the Id field
                GameAudioId? audioId = GameAudioId.TryCreate(dto.Id);
                if (audioId == null)
                {
                    _logger.LogWarning("Invalid audio ID format in: {File}", file);
                    continue;
                }

                // Extract category and subcategory from the audio ID
                string category = audioId.Category;
                string? subcategory = audioId.AudioSubcategory;

                // Convert DTO to entity
                var audioDef = new AudioEntity
                {
                    AudioId = audioId,
                    DisplayName = dto.DisplayName ?? audioId.Name,
                    AudioPath = dto.AudioPath,
                    Category = category,
                    Subcategory = subcategory,
                    Volume = dto.Volume ?? 1.0f,
                    Loop = dto.Loop ?? true,
                    FadeIn = dto.FadeIn ?? 0.0f,
                    FadeOut = dto.FadeOut ?? 0.0f,
                    LoopStartSamples = dto.LoopStartSamples,
                    LoopLengthSamples = dto.LoopLengthSamples,
                    LoopStartSec = dto.LoopStartSec,
                    LoopEndSec = dto.LoopEndSec,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.Audios.Add(audioDef);
                count++;

                _logger.LogDebug("Loaded audio definition: {AudioId}", audioDef.AudioId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load audio definition: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} audio definitions", count);
        return count;
    }

    // ============================================================================
    // NEW: Unified Definition Loading Methods
    // ============================================================================

    /// <summary>
    ///     Load sprite definitions from JSON files into EF Core.
    ///     Replaces SpriteRegistry JSON loading.
    /// </summary>
    private async Task<int> LoadSpriteDefinitionsAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("Sprites", "*.json");
            _logger.LogDebug("Using ContentProvider for Sprites - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("SpriteDefinitions", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                SpriteDefinitionDto? dto = JsonSerializer.Deserialize<SpriteDefinitionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize sprite definition: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
                {
                    _logger.LogWarning("Sprite definition missing required fields: {File}", file);
                    continue;
                }

                // Parse GameSpriteId from the Id field
                GameSpriteId? spriteId = GameSpriteId.TryCreate(dto.Id);
                if (spriteId == null)
                {
                    _logger.LogWarning("Invalid sprite ID format in: {File}", file);
                    continue;
                }

                // Convert DTO to entity with typed collections (EF Core handles JSON serialization)
                var spriteDef = new SpriteEntity
                {
                    SpriteId = spriteId,
                    DisplayName = dto.DisplayName ?? spriteId.Name,
                    Type = dto.Type ?? "Sprite",
                    TexturePath = dto.TexturePath,
                    FrameWidth = dto.FrameWidth ?? 16,
                    FrameHeight = dto.FrameHeight ?? 32,
                    FrameCount = dto.FrameCount ?? 1,
                    // Map DTO frames to owned entity type
                    Frames = dto.Frames?.Select(f => new SpriteFrame
                    {
                        Index = f.Index,
                        X = f.X,
                        Y = f.Y,
                        Width = f.Width,
                        Height = f.Height
                    }).ToList() ?? new List<SpriteFrame>(),
                    // Map DTO animations to owned entity type
                    Animations = dto.Animations?.Select(a => new SpriteAnimation
                    {
                        Name = a.Name ?? string.Empty,
                        Loop = a.Loop,
                        FrameIndices = a.FrameIndices?.ToList() ?? new List<int>(),
                        FrameDurations = a.FrameDurations?.ToList() ?? new List<double>(),
                        FlipHorizontal = a.FlipHorizontal
                    }).ToList() ?? new List<SpriteAnimation>(),
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.Sprites.Add(spriteDef);
                count++;

                _logger.LogDebug("Loaded sprite definition: {SpriteId}", spriteDef.SpriteId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load sprite definition: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} sprite definitions", count);
        return count;
    }

    /// <summary>
    ///     Load popup background definitions from JSON files into EF Core.
    ///     Replaces PopupRegistry background JSON loading.
    /// </summary>
    private async Task<int> LoadPopupBackgroundsAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("PopupBackgrounds", "*.json");
            _logger.LogDebug("Using ContentProvider for PopupBackgrounds - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("PopupBackgrounds", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                PopupBackgroundDto? dto = JsonSerializer.Deserialize<PopupBackgroundDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize popup background: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
                {
                    _logger.LogWarning("Popup background missing required fields: {File}", file);
                    continue;
                }

                // Convert DTO to entity - TryCreate parses full IDs, Create for short names
                var backgroundDef = new PopupBackgroundEntity
                {
                    BackgroundId = GamePopupBackgroundId.TryCreate(dto.Id) ?? GamePopupBackgroundId.Create(dto.Id),
                    DisplayName = dto.DisplayName ?? Path.GetFileNameWithoutExtension(file),
                    Type = dto.Type ?? "Bitmap",
                    TexturePath = dto.TexturePath,
                    Width = dto.Width ?? 80,
                    Height = dto.Height ?? 24,
                    Description = dto.Description,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.PopupBackgrounds.Add(backgroundDef);
                count++;

                _logger.LogDebug("Loaded popup background: {BackgroundId}", backgroundDef.BackgroundId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load popup background: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} popup backgrounds", count);
        return count;
    }

    /// <summary>
    ///     Load popup outline definitions from JSON files into EF Core.
    ///     Replaces PopupRegistry outline JSON loading.
    /// </summary>
    private async Task<int> LoadPopupOutlinesAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("PopupOutlines", "*.json");
            _logger.LogDebug("Using ContentProvider for PopupOutlines - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("PopupOutlines", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                PopupOutlineDto? dto = JsonSerializer.Deserialize<PopupOutlineDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize popup outline: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
                {
                    _logger.LogWarning("Popup outline missing required fields: {File}", file);
                    continue;
                }

                // Convert DTO to entity with typed collections (EF Core handles JSON serialization)
                // TryCreate parses full IDs, Create for short names
                var outlineDef = new PopupOutlineEntity
                {
                    OutlineId = GamePopupOutlineId.TryCreate(dto.Id) ?? GamePopupOutlineId.Create(dto.Id),
                    DisplayName = dto.DisplayName ?? Path.GetFileNameWithoutExtension(file),
                    Type = dto.Type ?? "TileSheet",
                    TexturePath = dto.TexturePath,
                    TileWidth = dto.TileWidth ?? 8,
                    TileHeight = dto.TileHeight ?? 8,
                    TileCount = dto.TileCount ?? 0,
                    // Map DTO tiles to owned entity type
                    Tiles = dto.Tiles?.Select(t => new OutlineTile
                    {
                        Index = t.Index,
                        X = t.X,
                        Y = t.Y,
                        Width = t.Width,
                        Height = t.Height
                    }).ToList() ?? new List<OutlineTile>(),
                    // Map DTO tile usage to owned entity type
                    TileUsage = dto.TileUsage != null ? new OutlineTileUsage
                    {
                        TopEdge = dto.TileUsage.TopEdge?.ToList() ?? new List<int>(),
                        LeftEdge = dto.TileUsage.LeftEdge?.ToList() ?? new List<int>(),
                        RightEdge = dto.TileUsage.RightEdge?.ToList() ?? new List<int>(),
                        BottomEdge = dto.TileUsage.BottomEdge?.ToList() ?? new List<int>()
                    } : null,
                    CornerWidth = dto.CornerWidth ?? 8,
                    CornerHeight = dto.CornerHeight ?? 8,
                    BorderWidth = dto.BorderWidth ?? 8,
                    BorderHeight = dto.BorderHeight ?? 8,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                _context.PopupOutlines.Add(outlineDef);
                count++;

                _logger.LogDebug("Loaded popup outline: {OutlineId}", outlineDef.OutlineId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load popup outline: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} popup outlines", count);
        return count;
    }

    /// <summary>
    ///     Load behavior definitions from JSON files into EF Core.
    ///     Replaces TypeRegistry&lt;BehaviorDefinition&gt;.
    ///     Uses ContentProvider for mod-aware loading when available.
    /// </summary>
    private async Task<int> LoadBehaviorDefinitionsAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("Behaviors", "*.json");
            _logger.LogDebug("Using ContentProvider for Behaviors - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("BehaviorDefinitions", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }

        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                BehaviorDefinitionDto? dto = JsonSerializer.Deserialize<BehaviorDefinitionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize behavior definition: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    _logger.LogWarning("Behavior definition missing required fields: {File}", file);
                    continue;
                }

                // Detect source mod from file path (if file is under Mods/ directory)
                string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);

                // Convert DTO to entity
                // Use TryCreate first for full ID format, fall back to Create for simple names
                var behaviorDef = new BehaviorEntity
                {
                    BehaviorId = GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id),
                    DisplayName = dto.DisplayName ?? dto.Id,
                    Description = dto.Description,
                    DefaultSpeed = dto.DefaultSpeed ?? 4.0f,
                    PauseAtWaypoint = dto.PauseAtWaypoint ?? 1.0f,
                    AllowInteractionWhileMoving = dto.AllowInteractionWhileMoving ?? false,
                    BehaviorScript = dto.BehaviorScript,
                    SourceMod = sourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                if (sourceMod != null)
                {
                    _logger.LogDebug("Loaded mod-overridden behavior: {Id} from {Mod}", dto.Id, sourceMod);
                }

                _context.Behaviors.Add(behaviorDef);
                count++;

                _logger.LogDebug("Loaded behavior definition: {BehaviorId}", behaviorDef.BehaviorId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load behavior definition: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} behavior definitions", count);
        return count;
    }

    /// <summary>
    ///     Load tile behavior definitions from JSON files into EF Core.
    ///     Replaces TypeRegistry&lt;TileBehaviorDefinition&gt;.
    ///     Uses ContentProvider for mod-aware loading when available.
    /// </summary>
    private async Task<int> LoadTileBehaviorDefinitionsAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            files = _contentProvider.GetAllContentPaths("TileBehaviors", "*.json");
            _logger.LogDebug("Using ContentProvider for TileBehaviors - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("TileBehaviorDefinitions", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }

        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                TileBehaviorDefinitionDto? dto = JsonSerializer.Deserialize<TileBehaviorDefinitionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize tile behavior definition: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    _logger.LogWarning("Tile behavior definition missing required fields: {File}", file);
                    continue;
                }

                // Parse flags from string (e.g., "ForcesMovement, DisablesRunning")
                int flags = 0;
                if (!string.IsNullOrWhiteSpace(dto.Flags))
                {
                    flags = ParseTileBehaviorFlags(dto.Flags);
                }

                // Detect source mod from file path (if file is under Mods/ directory)
                string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);

                // Serialize extension data from mods (testProperty, modded, etc.)
                string? extensionDataJson = null;
                if (dto.ExtensionData != null && dto.ExtensionData.Count > 0)
                {
                    extensionDataJson = JsonSerializer.Serialize(dto.ExtensionData, _jsonOptions);
                }

                // Convert DTO to entity
                // Use TryCreate first for full ID format, fall back to Create for simple names
                var tileBehaviorDef = new TileBehaviorEntity
                {
                    TileBehaviorId = GameTileBehaviorId.TryCreate(dto.Id) ?? GameTileBehaviorId.Create(dto.Id),
                    DisplayName = dto.DisplayName ?? dto.Id,
                    Description = dto.Description,
                    Flags = flags,
                    BehaviorScript = dto.BehaviorScript,
                    SourceMod = sourceMod,
                    Version = dto.Version ?? "1.0.0",
                    ExtensionData = extensionDataJson
                };

                if (sourceMod != null)
                {
                    _logger.LogDebug("Loaded mod-overridden tile behavior: {Id} from {Mod}", dto.Id, sourceMod);
                    if (extensionDataJson != null)
                    {
                        _logger.LogDebug("  Extension data: {ExtensionData}", extensionDataJson);
                    }
                }

                _context.TileBehaviors.Add(tileBehaviorDef);
                count++;

                _logger.LogDebug("Loaded tile behavior definition: {TileBehaviorId}", tileBehaviorDef.TileBehaviorId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tile behavior definition: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} tile behavior definitions", count);
        return count;
    }

    /// <summary>
    ///     Load font definitions from JSON files into EF Core.
    ///     Replaces FontLoader hardcoded constants.
    ///     Uses ContentProvider for mod-aware loading when available.
    /// </summary>
    private async Task<int> LoadFontDefinitionsAsync(string path, CancellationToken ct)
    {
        // Use ContentProvider for mod-aware loading (handles mod overrides)
        IEnumerable<string> files;
        if (_contentProvider != null)
        {
            // GetAllContentPaths returns files from mods (by priority) then base game
            // Files with same relative path are deduplicated (mod wins over base)
            // NOTE: Uses "FontDefinitions" (Definitions/Fonts) not "Fonts" (Assets/Fonts where TTF files are)
            files = _contentProvider.GetAllContentPaths("FontDefinitions", "*.json");
            _logger.LogDebug("Using ContentProvider for FontDefinitions - found {Count} files", files.Count());
        }
        else
        {
            // Fallback: direct file system access (no mod support)
            if (!Directory.Exists(path))
            {
                _logger.LogDirectoryNotFound("FontDefinitions", path);
                return 0;
            }
            files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsHiddenOrSystemDirectory(f));
        }

        int count = 0;

        // OPTIMIZATION: Load all existing fonts once to avoid N+1 queries and support overrides
        Dictionary<GameFontId, FontEntity> existingFonts = await _context
            .Fonts.AsNoTracking()
            .ToDictionaryAsync(f => f.FontId, ct);

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                FontDefinitionDto? dto = JsonSerializer.Deserialize<FontDefinitionDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize font definition: {File}", file);
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.FontPath))
                {
                    _logger.LogWarning("Font definition missing required fields: {File}", file);
                    continue;
                }

                // Parse and validate GameFontId from the Id field
                GameFontId? fontId = GameFontId.TryCreate(dto.Id);
                if (fontId == null)
                {
                    _logger.LogWarning("Invalid font ID format in: {File}", file);
                    continue;
                }

                // Detect source mod from file path (if file is under Mods/ directory)
                string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);

                // Convert DTO to entity
                var fontDef = new FontEntity
                {
                    FontId = fontId,
                    DisplayName = dto.DisplayName ?? dto.Id,
                    Description = dto.Description,
                    FontPath = dto.FontPath,
                    Category = dto.Category ?? "game",
                    DefaultSize = dto.DefaultSize ?? 16,
                    LineSpacing = dto.LineSpacing ?? 1.0f,
                    CharacterSpacing = dto.CharacterSpacing ?? 0.0f,
                    SupportsUnicode = dto.SupportsUnicode ?? true,
                    IsMonospace = dto.IsMonospace ?? false,
                    SourceMod = sourceMod,
                    Version = dto.Version ?? "1.0.0"
                };

                // Support mod overrides - if font already exists, update it
                if (existingFonts.TryGetValue(fontDef.FontId, out FontEntity? existing))
                {
                    _context.Fonts.Attach(existing);
                    _context.Entry(existing).CurrentValues.SetValues(fontDef);
                    _logger.LogDebug("Font overridden: {FontId} by {Source}", fontDef.FontId, sourceMod ?? "base");
                }
                else
                {
                    _context.Fonts.Add(fontDef);
                }

                count++;

                _logger.LogDebug("Loaded font definition: {FontId}", fontDef.FontId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load font definition: {File}", file);
            }
        }

        // Save to in-memory database
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded {Count} font definitions", count);
        return count;
    }

    /// <summary>
    ///     Parses TileBehaviorFlags from a comma-separated string.
    /// </summary>
    private static int ParseTileBehaviorFlags(string flagsString)
    {
        int result = 0;

        // Parse each flag name and combine using bitwise OR
        string[] flagNames = flagsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string flagName in flagNames)
        {
            if (Enum.TryParse<TileBehaviorFlags>(flagName, ignoreCase: true, out TileBehaviorFlags flag))
            {
                result |= (int)flag;
            }
        }

        return result;
    }

    /// <summary>
    ///     Determines the source mod/base from a file path.
    ///     Uses cross-platform path detection.
    /// </summary>
    private static string GetSourceFromPath(string filePath)
    {
        // Normalize path separators for cross-platform compatibility
        string normalizedPath = filePath.Replace('\\', '/');

        // Check for Mods directory in the path
        const string modsMarker = "/Mods/";
        int modsIndex = normalizedPath.IndexOf(modsMarker, StringComparison.OrdinalIgnoreCase);

        if (modsIndex >= 0)
        {
            // Extract the mod folder name (first segment after /Mods/)
            string afterMods = normalizedPath.Substring(modsIndex + modsMarker.Length);
            int nextSeparator = afterMods.IndexOf('/');

            if (nextSeparator > 0)
            {
                return afterMods.Substring(0, nextSeparator);
            }
            else if (afterMods.Length > 0)
            {
                return afterMods;
            }
        }

        return "base";
    }

    /// <summary>
    ///     Detects the source mod from a file path.
    ///     Returns null for base game files, mod folder name for mod files.
    /// </summary>
    private static string? DetectSourceModFromPath(string filePath)
    {
        string source = GetSourceFromPath(filePath);
        return source == "base" ? null : source;
    }

    /// <summary>
    ///     Gets the content provider for mod-aware loading.
    /// </summary>
    public IContentProvider? ContentProvider => _contentProvider;
}

#region DTOs for JSON Deserialization

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

/// <summary>
///     DTO for deserializing PopupTheme JSON files.
/// </summary>
internal record PopupThemeDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Background { get; init; }
    public string? Outline { get; init; }
    public int? UsageCount { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing MapSection JSON files.
/// </summary>
internal record MapSectionDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Theme { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing MapEntity JSON files.
///     Simple schema: Id, DisplayName, Type, Region, Description, TiledPath
/// </summary>
internal record MapEntityDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Type { get; init; }
    public string? Region { get; init; }
    public string? Description { get; init; }
    public string? TiledPath { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing AudioEntity JSON files.
///     Matches the format generated by porycon audio extraction.
/// </summary>
internal record AudioEntityDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? AudioPath { get; init; }
    public float? Volume { get; init; }
    public bool? Loop { get; init; }
    public float? FadeIn { get; init; }
    public float? FadeOut { get; init; }
    public int? LoopStartSamples { get; init; }
    public int? LoopLengthSamples { get; init; }
    public float? LoopStartSec { get; init; }
    public float? LoopEndSec { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing SpriteDefinition JSON files.
/// </summary>
internal record SpriteDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Type { get; init; }
    public string? TexturePath { get; init; }
    public int? FrameWidth { get; init; }
    public int? FrameHeight { get; init; }
    public int? FrameCount { get; init; }
    public List<SpriteFrameDto>? Frames { get; init; }
    public List<SpriteAnimationDto>? Animations { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for sprite frame definition.
/// </summary>
internal record SpriteFrameDto
{
    public int Index { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
///     DTO for sprite animation definition.
/// </summary>
internal record SpriteAnimationDto
{
    public string? Name { get; init; }
    public bool Loop { get; init; }
    public List<int>? FrameIndices { get; init; }
    public List<double>? FrameDurations { get; init; }
    public bool FlipHorizontal { get; init; }
}

/// <summary>
///     DTO for deserializing PopupBackground JSON files.
/// </summary>
internal record PopupBackgroundDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Type { get; init; }
    public string? TexturePath { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? Description { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing PopupOutline JSON files.
/// </summary>
internal record PopupOutlineDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Type { get; init; }
    public string? TexturePath { get; init; }
    public int? TileWidth { get; init; }
    public int? TileHeight { get; init; }
    public int? TileCount { get; init; }
    public List<OutlineTileDto>? Tiles { get; init; }
    public OutlineTileUsageDto? TileUsage { get; init; }
    public int? CornerWidth { get; init; }
    public int? CornerHeight { get; init; }
    public int? BorderWidth { get; init; }
    public int? BorderHeight { get; init; }
    public string? Description { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for outline tile definition.
/// </summary>
internal record OutlineTileDto
{
    public int Index { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
///     DTO for outline tile usage mapping.
/// </summary>
internal record OutlineTileUsageDto
{
    public List<int>? TopEdge { get; init; }
    public List<int>? LeftEdge { get; init; }
    public List<int>? RightEdge { get; init; }
    public List<int>? BottomEdge { get; init; }
}

/// <summary>
///     DTO for deserializing BehaviorDefinition JSON files.
/// </summary>
internal record BehaviorDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public float? DefaultSpeed { get; init; }
    public float? PauseAtWaypoint { get; init; }
    public bool? AllowInteractionWhileMoving { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing TileBehaviorDefinition JSON files.
///     Supports mod extension data via JsonExtensionData.
/// </summary>
internal record TileBehaviorDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public string? Flags { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    /// <summary>
    ///     Captures any additional properties from mods (e.g., testProperty, modded).
    ///     These are stored in the entity's ExtensionData column.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
///     DTO for deserializing FontDefinition JSON files.
/// </summary>
internal record FontDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? FontPath { get; init; }
    public string? Category { get; init; }
    public int? DefaultSize { get; init; }
    public float? LineSpacing { get; init; }
    public float? CharacterSpacing { get; init; }
    public bool? SupportsUnicode { get; init; }
    public bool? IsMonospace { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}

#endregion
