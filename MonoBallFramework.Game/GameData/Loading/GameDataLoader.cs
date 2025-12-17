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
///     Uses a generic loading pattern to reduce code duplication across entity types.
/// </summary>
public class GameDataLoader
{
    private readonly GameDataContext _context;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<GameDataLoader> _logger;

    public GameDataLoader(
        GameDataContext context,
        ILogger<GameDataLoader> logger,
        IContentProvider contentProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ContentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };
    }

    /// <summary>
    ///     Gets the content provider for mod-aware loading.
    /// </summary>
    public IContentProvider ContentProvider { get; }

    /// <summary>
    ///     Load all game data from JSON files.
    ///     Uses ContentProvider for mod-aware path resolution - paths are resolved automatically
    ///     based on content type mappings in mod.json files.
    /// </summary>
    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Loading game data definitions via ContentProvider...");

        var loadedCounts = new Dictionary<string, int>();

        // All loading methods use ContentProvider.GetAllContentPaths() internally
        // Content types are mapped in mod.json contentFolders configuration
        loadedCounts["Maps"] = await LoadMapEntitysAsync(ct);
        loadedCounts["PopupThemes"] = await LoadPopupThemesAsync(ct);
        loadedCounts["MapSections"] = await LoadMapSectionsAsync(ct);
        loadedCounts["Audios"] = await LoadAudioEntitysAsync(ct);
        loadedCounts["Sprites"] = await LoadSpriteDefinitionsAsync(ct);
        loadedCounts["PopupBackgrounds"] = await LoadPopupBackgroundsAsync(ct);
        loadedCounts["PopupOutlines"] = await LoadPopupOutlinesAsync(ct);
        loadedCounts["Behaviors"] = await LoadBehaviorDefinitionsAsync(ct);
        loadedCounts["TileBehaviors"] = await LoadTileBehaviorDefinitionsAsync(ct);
        loadedCounts["Fonts"] = await LoadFontDefinitionsAsync(ct);

        // Log summary
        _logger.LogGameDataLoaded(loadedCounts);
    }

    /// <summary>
    ///     Load map definitions from JSON files.
    ///     Simple schema: Id, DisplayName, Type, Region, Description, TiledPath.
    ///     Gameplay metadata (music, weather, connections) is read from Tiled at runtime.
    /// </summary>
    private async Task<int> LoadMapEntitysAsync(CancellationToken ct)
    {
        // Pre-load existing maps for override support
        Dictionary<GameMapId, MapEntity> existingMaps = await _context
            .Maps.AsNoTracking()
            .ToDictionaryAsync(m => m.MapId, ct);

        return await LoadDefinitionsAsync<MapEntityDto, MapEntity, GameMapId>(
            "MapDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TiledPath))
                {
                    return new ParseResult<MapEntity, GameMapId>(null!, default!, false, "Missing Id or TiledPath");
                }

                var gameMapId = GameMapId.TryCreate(dto.Id);
                if (gameMapId == null)
                {
                    return new ParseResult<MapEntity, GameMapId>(null!, default!, false, "Invalid map ID format");
                }

                var mapDef = new MapEntity
                {
                    MapId = gameMapId,
                    Name = dto.Name ?? gameMapId.Name,
                    Region = dto.Region ?? "hoenn",
                    MapType = dto.Type,
                    TiledDataPath = dto.TiledPath,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };
                return new ParseResult<MapEntity, GameMapId>(mapDef, gameMapId, true);
            },
            entity => _context.Maps.Add(entity),
            existingMaps,
            (existing, newEntity) =>
            {
                _context.Maps.Attach(existing);
                _context.Entry(existing).CurrentValues.SetValues(newEntity);
            },
            ct);
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
        return props.TryGetValue(key, out object? value) ? value?.ToString() : null;
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
        return pathParts.Any(part => part.StartsWith('.'));
    }

    /// <summary>
    ///     Load popup theme definitions from JSON files.
    /// </summary>
    private Task<int> LoadPopupThemesAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<PopupThemeDto, PopupThemeEntity, GameThemeId>(
            "PopupThemeDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    return new ParseResult<PopupThemeEntity, GameThemeId>(null!, default!, false, "Missing Id");
                }

                GameThemeId themeId = GameThemeId.TryCreate(dto.Id) ?? GameThemeId.Create(dto.Id);
                var theme = new PopupThemeEntity
                {
                    ThemeId = themeId,
                    Name = dto.Name ?? dto.Id,
                    Description = dto.Description,
                    Background = dto.Background ?? dto.Id,
                    Outline = dto.Outline ?? $"{dto.Id}_outline",
                    UsageCount = dto.UsageCount ?? 0,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };
                return new ParseResult<PopupThemeEntity, GameThemeId>(theme, themeId, true);
            },
            entity => _context.PopupThemes.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load map section definitions from JSON files.
    /// </summary>
    private Task<int> LoadMapSectionsAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<MapSectionDto, MapSectionEntity, GameMapSectionId>(
            "MapSectionDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Theme))
                {
                    return new ParseResult<MapSectionEntity, GameMapSectionId>(null!, default!, false,
                        "Missing Id or Theme");
                }

                GameMapSectionId sectionId = GameMapSectionId.TryCreate(dto.Id) ?? GameMapSectionId.Create(dto.Id);
                var section = new MapSectionEntity
                {
                    MapSectionId = sectionId,
                    Name = dto.Name ?? dto.Id,
                    ThemeId = GameThemeId.TryCreate(dto.Theme) ?? GameThemeId.Create(dto.Theme),
                    X = dto.X,
                    Y = dto.Y,
                    Width = dto.Width,
                    Height = dto.Height,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };
                return new ParseResult<MapSectionEntity, GameMapSectionId>(section, sectionId, true);
            },
            entity => _context.MapSections.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load audio definitions from JSON files.
    ///     Recursively processes all subdirectories (Music/Battle, Music/Towns, SFX/Battle, etc.).
    /// </summary>
    private Task<int> LoadAudioEntitysAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<AudioEntityDto, AudioEntity, GameAudioId>(
            "AudioDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.AudioPath))
                {
                    return new ParseResult<AudioEntity, GameAudioId>(null!, default!, false, "Missing Id or AudioPath");
                }

                var audioId = GameAudioId.TryCreate(dto.Id);
                if (audioId == null)
                {
                    return new ParseResult<AudioEntity, GameAudioId>(null!, default!, false, "Invalid audio ID format");
                }

                var audioDef = new AudioEntity
                {
                    AudioId = audioId,
                    Name = dto.Name ?? audioId.Name,
                    AudioPath = dto.AudioPath,
                    Category = audioId.Category,
                    Subcategory = audioId.AudioSubcategory,
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
                return new ParseResult<AudioEntity, GameAudioId>(audioDef, audioId, true);
            },
            entity => _context.Audios.Add(entity),
            ct: ct);
    }

    // ============================================================================
    // NEW: Unified Definition Loading Methods
    // ============================================================================

    /// <summary>
    ///     Load sprite definitions from JSON files into EF Core.
    ///     Replaces SpriteRegistry JSON loading.
    /// </summary>
    private Task<int> LoadSpriteDefinitionsAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<SpriteDefinitionDto, SpriteEntity, GameSpriteId>(
            "SpriteDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
                {
                    return new ParseResult<SpriteEntity, GameSpriteId>(null!, default!, false,
                        "Missing Id or TexturePath");
                }

                var spriteId = GameSpriteId.TryCreate(dto.Id);
                if (spriteId == null)
                {
                    return new ParseResult<SpriteEntity, GameSpriteId>(null!, default!, false,
                        "Invalid sprite ID format");
                }

                var spriteDef = new SpriteEntity
                {
                    SpriteId = spriteId,
                    Name = dto.Name ?? spriteId.Name,
                    Type = dto.Type ?? "Sprite",
                    TexturePath = dto.TexturePath,
                    FrameWidth = dto.FrameWidth ?? 16,
                    FrameHeight = dto.FrameHeight ?? 32,
                    FrameCount = dto.FrameCount ?? 1,
                    Frames =
                        dto.Frames?.Select(f => new SpriteFrame
                        {
                            Index = f.Index,
                            X = f.X,
                            Y = f.Y,
                            Width = f.Width,
                            Height = f.Height
                        }).ToList() ?? new List<SpriteFrame>(),
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
                return new ParseResult<SpriteEntity, GameSpriteId>(spriteDef, spriteId, true);
            },
            entity => _context.Sprites.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load popup background definitions from JSON files into EF Core.
    ///     Replaces PopupRegistry background JSON loading.
    /// </summary>
    private Task<int> LoadPopupBackgroundsAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<PopupBackgroundDto, PopupBackgroundEntity, GamePopupBackgroundId>(
            "PopupBackgroundDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
                {
                    return new ParseResult<PopupBackgroundEntity, GamePopupBackgroundId>(null!, default!, false,
                        "Missing Id or TexturePath");
                }

                GamePopupBackgroundId backgroundId =
                    GamePopupBackgroundId.TryCreate(dto.Id) ?? GamePopupBackgroundId.Create(dto.Id);
                var backgroundDef = new PopupBackgroundEntity
                {
                    BackgroundId = backgroundId,
                    Name = dto.Name ?? Path.GetFileNameWithoutExtension(file),
                    Type = dto.Type ?? "Bitmap",
                    TexturePath = dto.TexturePath,
                    Width = dto.Width ?? 80,
                    Height = dto.Height ?? 24,
                    Description = dto.Description,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };
                return new ParseResult<PopupBackgroundEntity, GamePopupBackgroundId>(backgroundDef, backgroundId, true);
            },
            entity => _context.PopupBackgrounds.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load popup outline definitions from JSON files into EF Core.
    ///     Replaces PopupRegistry outline JSON loading.
    /// </summary>
    private Task<int> LoadPopupOutlinesAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<PopupOutlineDto, PopupOutlineEntity, GamePopupOutlineId>(
            "PopupOutlineDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
                {
                    return new ParseResult<PopupOutlineEntity, GamePopupOutlineId>(null!, default!, false,
                        "Missing Id or TexturePath");
                }

                GamePopupOutlineId outlineId =
                    GamePopupOutlineId.TryCreate(dto.Id) ?? GamePopupOutlineId.Create(dto.Id);
                var outlineDef = new PopupOutlineEntity
                {
                    OutlineId = outlineId,
                    Name = dto.Name ?? Path.GetFileNameWithoutExtension(file),
                    Type = dto.Type ?? "TileSheet",
                    TexturePath = dto.TexturePath,
                    TileWidth = dto.TileWidth ?? 8,
                    TileHeight = dto.TileHeight ?? 8,
                    TileCount = dto.TileCount ?? 0,
                    Tiles =
                        dto.Tiles?.Select(t => new OutlineTile
                        {
                            Index = t.Index,
                            X = t.X,
                            Y = t.Y,
                            Width = t.Width,
                            Height = t.Height
                        }).ToList() ?? new List<OutlineTile>(),
                    TileUsage =
                        dto.TileUsage != null
                            ? new OutlineTileUsage
                            {
                                TopEdge = dto.TileUsage.TopEdge?.ToList() ?? new List<int>(),
                                LeftEdge = dto.TileUsage.LeftEdge?.ToList() ?? new List<int>(),
                                RightEdge = dto.TileUsage.RightEdge?.ToList() ?? new List<int>(),
                                BottomEdge = dto.TileUsage.BottomEdge?.ToList() ?? new List<int>()
                            }
                            : null,
                    CornerWidth = dto.CornerWidth ?? 8,
                    CornerHeight = dto.CornerHeight ?? 8,
                    BorderWidth = dto.BorderWidth ?? 8,
                    BorderHeight = dto.BorderHeight ?? 8,
                    SourceMod = dto.SourceMod,
                    Version = dto.Version ?? "1.0.0"
                };
                return new ParseResult<PopupOutlineEntity, GamePopupOutlineId>(outlineDef, outlineId, true);
            },
            entity => _context.PopupOutlines.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load behavior definitions from JSON files into EF Core.
    ///     Replaces TypeRegistry&lt;BehaviorDefinition&gt;.
    ///     Uses ContentProvider for mod-aware loading when available.
    /// </summary>
    private Task<int> LoadBehaviorDefinitionsAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<BehaviorDefinitionDto, BehaviorEntity, GameBehaviorId>(
            "BehaviorDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    return new ParseResult<BehaviorEntity, GameBehaviorId>(null!, default!, false, "Missing Id");
                }

                string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);
                GameBehaviorId behaviorId = GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id);
                var behaviorDef = new BehaviorEntity
                {
                    BehaviorId = behaviorId,
                    Name = dto.Name ?? dto.Id,
                    Description = dto.Description,
                    DefaultSpeed = dto.DefaultSpeed ?? 4.0f,
                    PauseAtWaypoint = dto.PauseAtWaypoint ?? 1.0f,
                    AllowInteractionWhileMoving = dto.AllowInteractionWhileMoving ?? false,
                    BehaviorScript = dto.BehaviorScript,
                    SourceMod = sourceMod,
                    Version = dto.Version ?? "1.0.0"
                };
                return new ParseResult<BehaviorEntity, GameBehaviorId>(behaviorDef, behaviorId, true);
            },
            entity => _context.Behaviors.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load tile behavior definitions from JSON files into EF Core.
    ///     Replaces TypeRegistry&lt;TileBehaviorDefinition&gt;.
    ///     Uses ContentProvider for mod-aware loading when available.
    /// </summary>
    private Task<int> LoadTileBehaviorDefinitionsAsync(CancellationToken ct)
    {
        return LoadDefinitionsAsync<TileBehaviorDefinitionDto, TileBehaviorEntity, GameTileBehaviorId>(
            "TileBehaviorDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    return new ParseResult<TileBehaviorEntity, GameTileBehaviorId>(null!, default!, false,
                        "Missing Id");
                }

                int flags = !string.IsNullOrWhiteSpace(dto.Flags) ? ParseTileBehaviorFlags(dto.Flags) : 0;
                string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);
                string? extensionDataJson = dto.ExtensionData?.Count > 0
                    ? JsonSerializer.Serialize(dto.ExtensionData, _jsonOptions)
                    : null;

                GameTileBehaviorId tileBehaviorId =
                    GameTileBehaviorId.TryCreate(dto.Id) ?? GameTileBehaviorId.Create(dto.Id);
                var tileBehaviorDef = new TileBehaviorEntity
                {
                    TileBehaviorId = tileBehaviorId,
                    Name = dto.Name ?? dto.Id,
                    Description = dto.Description,
                    Flags = flags,
                    BehaviorScript = dto.BehaviorScript,
                    SourceMod = sourceMod,
                    Version = dto.Version ?? "1.0.0",
                    ExtensionData = extensionDataJson
                };
                return new ParseResult<TileBehaviorEntity, GameTileBehaviorId>(tileBehaviorDef, tileBehaviorId, true);
            },
            entity => _context.TileBehaviors.Add(entity),
            ct: ct);
    }

    /// <summary>
    ///     Load font definitions from JSON files into EF Core.
    ///     Replaces FontLoader hardcoded constants.
    ///     Uses ContentProvider for mod-aware loading when available.
    /// </summary>
    private async Task<int> LoadFontDefinitionsAsync(CancellationToken ct)
    {
        // Pre-load existing fonts for override support
        Dictionary<GameFontId, FontEntity> existingFonts = await _context
            .Fonts.AsNoTracking()
            .ToDictionaryAsync(f => f.FontId, ct);

        return await LoadDefinitionsAsync<FontDefinitionDto, FontEntity, GameFontId>(
            "FontDefinitions",
            (dto, file) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.FontPath))
                {
                    return new ParseResult<FontEntity, GameFontId>(null!, default!, false, "Missing Id or FontPath");
                }

                var fontId = GameFontId.TryCreate(dto.Id);
                if (fontId == null)
                {
                    return new ParseResult<FontEntity, GameFontId>(null!, default!, false, "Invalid font ID format");
                }

                string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);
                var fontDef = new FontEntity
                {
                    FontId = fontId,
                    Name = dto.Name ?? dto.Id,
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
                return new ParseResult<FontEntity, GameFontId>(fontDef, fontId, true);
            },
            entity => _context.Fonts.Add(entity),
            existingFonts,
            (existing, newEntity) =>
            {
                _context.Fonts.Attach(existing);
                _context.Entry(existing).CurrentValues.SetValues(newEntity);
            },
            ct);
    }

    /// <summary>
    ///     Parses TileBehaviorFlags from a comma-separated string.
    /// </summary>
    private static int ParseTileBehaviorFlags(string flagsString)
    {
        int result = 0;

        // Parse each flag name and combine using bitwise OR
        string[] flagNames =
            flagsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string flagName in flagNames)
        {
            if (Enum.TryParse(flagName, true, out TileBehaviorFlags flag))
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
            string afterMods = normalizedPath[(modsIndex + modsMarker.Length)..];
            int nextSeparator = afterMods.IndexOf('/');

            if (nextSeparator > 0)
            {
                return afterMods[..nextSeparator];
            }

            if (afterMods.Length > 0)
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

    #region Generic Definition Loading Infrastructure

    /// <summary>
    ///     Result of parsing a DTO into an entity with mod override information.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <typeparam name="TKey">Entity key type.</typeparam>
    private record ParseResult<TEntity, TKey>(TEntity Entity, TKey Key, bool Success, string? ErrorMessage = null);

    /// <summary>
    ///     Generic loader for definition files that handles the common pattern:
    ///     get files → read JSON → deserialize → validate → create entity → save.
    /// </summary>
    /// <typeparam name="TDto">DTO type for JSON deserialization.</typeparam>
    /// <typeparam name="TEntity">Entity type for EF Core.</typeparam>
    /// <typeparam name="TKey">Entity key type.</typeparam>
    /// <param name="contentType">ContentProvider content type key.</param>
    /// <param name="parseDto">Function to parse DTO into entity with key.</param>
    /// <param name="addEntity">Action to add entity to DbContext.</param>
    /// <param name="existingEntities">Optional dictionary of existing entities for override support.</param>
    /// <param name="handleOverride">Optional action to handle entity override (update existing).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of entities loaded.</returns>
    private async Task<int> LoadDefinitionsAsync<TDto, TEntity, TKey>(
        string contentType,
        Func<TDto, string, ParseResult<TEntity, TKey>> parseDto,
        Action<TEntity> addEntity,
        Dictionary<TKey, TEntity>? existingEntities = null,
        Action<TEntity, TEntity>? handleOverride = null,
        CancellationToken ct = default)
        where TDto : class
        where TEntity : class
        where TKey : notnull
    {
        IEnumerable<string> files = ContentProvider.GetAllContentPaths(contentType);
        _logger.LogDebug("Using ContentProvider for {ContentType} - found {Count} files", contentType, files.Count());
        int count = 0;

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            // CA1031: File I/O and JSON parsing can throw many exception types; catching general Exception is intentional
#pragma warning disable CA1031
            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                TDto? dto = JsonSerializer.Deserialize<TDto>(json, _jsonOptions);

                if (dto == null)
                {
                    _logger.LogWarning("Failed to deserialize {ContentType}: {File}", contentType, file);
                    continue;
                }

                ParseResult<TEntity, TKey> result = parseDto(dto, file);
                if (!result.Success)
                {
                    _logger.LogWarning("{ContentType} validation failed for {File}: {Error}",
                        contentType, file, result.ErrorMessage ?? "Unknown error");
                    continue;
                }

                // Handle mod override if existing entities provided
                if (existingEntities != null && handleOverride != null &&
                    existingEntities.TryGetValue(result.Key, out TEntity? existing))
                {
                    handleOverride(existing, result.Entity);
                    _logger.LogDebug("{ContentType} overridden: {Key}", contentType, result.Key);
                }
                else
                {
                    addEntity(result.Entity);
                }

                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load {ContentType}: {File}", contentType, file);
            }
#pragma warning restore CA1031
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Loaded {Count} {ContentType}", count, contentType);
        return count;
    }

    #endregion
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
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("background")] public string? Background { get; init; }
    [JsonPropertyName("outline")] public string? Outline { get; init; }
    [JsonPropertyName("usageCount")] public int? UsageCount { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing MapSection JSON files.
/// </summary>
internal record MapSectionDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("theme")] public string? Theme { get; init; }
    [JsonPropertyName("x")] public int? X { get; init; }
    [JsonPropertyName("y")] public int? Y { get; init; }
    [JsonPropertyName("width")] public int? Width { get; init; }
    [JsonPropertyName("height")] public int? Height { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing MapEntity JSON files.
///     Simple schema: Id, Name, Type, Region, Description, TiledPath
/// </summary>
internal record MapEntityDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("region")] public string? Region { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("tiledPath")] public string? TiledPath { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing AudioEntity JSON files.
///     Matches the format generated by porycon audio extraction.
/// </summary>
internal record AudioEntityDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("audioPath")] public string? AudioPath { get; init; }
    [JsonPropertyName("volume")] public float? Volume { get; init; }
    [JsonPropertyName("loop")] public bool? Loop { get; init; }
    [JsonPropertyName("fadeIn")] public float? FadeIn { get; init; }
    [JsonPropertyName("fadeOut")] public float? FadeOut { get; init; }
    [JsonPropertyName("loopStartSamples")] public int? LoopStartSamples { get; init; }

    [JsonPropertyName("loopLengthSamples")]
    public int? LoopLengthSamples { get; init; }

    [JsonPropertyName("loopStartSec")] public float? LoopStartSec { get; init; }
    [JsonPropertyName("loopEndSec")] public float? LoopEndSec { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing SpriteDefinition JSON files.
/// </summary>
internal record SpriteDefinitionDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("texturePath")] public string? TexturePath { get; init; }
    [JsonPropertyName("frameWidth")] public int? FrameWidth { get; init; }
    [JsonPropertyName("frameHeight")] public int? FrameHeight { get; init; }
    [JsonPropertyName("frameCount")] public int? FrameCount { get; init; }
    [JsonPropertyName("frames")] public List<SpriteFrameDto>? Frames { get; init; }
    [JsonPropertyName("animations")] public List<SpriteAnimationDto>? Animations { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for sprite frame definition.
/// </summary>
internal record SpriteFrameDto
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("x")] public int X { get; init; }
    [JsonPropertyName("y")] public int Y { get; init; }
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
}

/// <summary>
///     DTO for sprite animation definition.
/// </summary>
internal record SpriteAnimationDto
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("loop")] public bool Loop { get; init; }
    [JsonPropertyName("frameIndices")] public List<int>? FrameIndices { get; init; }
    [JsonPropertyName("frameDurations")] public List<double>? FrameDurations { get; init; }
    [JsonPropertyName("flipHorizontal")] public bool FlipHorizontal { get; init; }
}

/// <summary>
///     DTO for deserializing PopupBackground JSON files.
/// </summary>
internal record PopupBackgroundDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("texturePath")] public string? TexturePath { get; init; }
    [JsonPropertyName("width")] public int? Width { get; init; }
    [JsonPropertyName("height")] public int? Height { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing PopupOutline JSON files.
/// </summary>
internal record PopupOutlineDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("texturePath")] public string? TexturePath { get; init; }
    [JsonPropertyName("tileWidth")] public int? TileWidth { get; init; }
    [JsonPropertyName("tileHeight")] public int? TileHeight { get; init; }
    [JsonPropertyName("tileCount")] public int? TileCount { get; init; }
    [JsonPropertyName("tiles")] public List<OutlineTileDto>? Tiles { get; init; }
    [JsonPropertyName("tileUsage")] public OutlineTileUsageDto? TileUsage { get; init; }
    [JsonPropertyName("cornerWidth")] public int? CornerWidth { get; init; }
    [JsonPropertyName("cornerHeight")] public int? CornerHeight { get; init; }
    [JsonPropertyName("borderWidth")] public int? BorderWidth { get; init; }
    [JsonPropertyName("borderHeight")] public int? BorderHeight { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for outline tile definition.
/// </summary>
internal record OutlineTileDto
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("x")] public int X { get; init; }
    [JsonPropertyName("y")] public int Y { get; init; }
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
}

/// <summary>
///     DTO for outline tile usage mapping.
/// </summary>
internal record OutlineTileUsageDto
{
    [JsonPropertyName("topEdge")] public List<int>? TopEdge { get; init; }
    [JsonPropertyName("leftEdge")] public List<int>? LeftEdge { get; init; }
    [JsonPropertyName("rightEdge")] public List<int>? RightEdge { get; init; }
    [JsonPropertyName("bottomEdge")] public List<int>? BottomEdge { get; init; }
}

/// <summary>
///     DTO for deserializing BehaviorDefinition JSON files.
/// </summary>
internal record BehaviorDefinitionDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("behaviorScript")] public string? BehaviorScript { get; init; }
    [JsonPropertyName("defaultSpeed")] public float? DefaultSpeed { get; init; }
    [JsonPropertyName("pauseAtWaypoint")] public float? PauseAtWaypoint { get; init; }

    [JsonPropertyName("allowInteractionWhileMoving")]
    public bool? AllowInteractionWhileMoving { get; init; }

    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

/// <summary>
///     DTO for deserializing TileBehaviorDefinition JSON files.
///     Supports mod extension data via JsonExtensionData.
/// </summary>
internal record TileBehaviorDefinitionDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("behaviorScript")] public string? BehaviorScript { get; init; }
    [JsonPropertyName("flags")] public string? Flags { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }

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
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("fontPath")] public string? FontPath { get; init; }
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("defaultSize")] public int? DefaultSize { get; init; }
    [JsonPropertyName("lineSpacing")] public float? LineSpacing { get; init; }
    [JsonPropertyName("characterSpacing")] public float? CharacterSpacing { get; init; }
    [JsonPropertyName("supportsUnicode")] public bool? SupportsUnicode { get; init; }
    [JsonPropertyName("isMonospace")] public bool? IsMonospace { get; init; }
    [JsonPropertyName("sourceMod")] public string? SourceMod { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
}

#endregion
