using MonoBallFramework.Game.Engine.Core.Exceptions;

namespace MonoBallFramework.Game.GameData.Exceptions;

/// <summary>
///     Base exception for all data loading and parsing errors.
///     Used for map data, NPC data, trainer data, and other game content.
/// </summary>
public abstract class DataException : MonoBallFrameworkException
{
    protected DataException(string errorCode, string message)
        : base(errorCode, message)
    {
    }

    protected DataException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException)
    {
    }

    public override string GetUserFriendlyMessage()
    {
        return "Failed to load game data. The game files may be corrupted.";
    }
}

/// <summary>
///     Exception thrown when map data fails to load or parse.
/// </summary>
public class MapLoadException : DataException
{
    public MapLoadException(string mapId, string message)
        : base("DATA_MAP_LOAD_FAILED", message)
    {
        WithContext("MapId", mapId);
    }

    public MapLoadException(string mapId, string message, Exception innerException)
        : base("DATA_MAP_LOAD_FAILED", message, innerException)
    {
        WithContext("MapId", mapId);
    }

    /// <summary>
    ///     Gets the map identifier that failed to load.
    /// </summary>
    public string MapId => Context.TryGetValue("MapId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can fallback to default map

    public override string GetUserFriendlyMessage()
    {
        return $"Failed to load map '{MapId}'. Returning to previous location.";
    }
}

/// <summary>
///     Exception thrown when a map file is not found.
/// </summary>
public class MapNotFoundException : DataException
{
    public MapNotFoundException(string mapId, string expectedPath)
        : base("DATA_MAP_NOT_FOUND", $"Map file not found: {expectedPath}")
    {
        WithContext("MapId", mapId).WithContext("ExpectedPath", expectedPath);
    }

    public string MapId => Context.TryGetValue("MapId", out object? id) ? id?.ToString() ?? "" : "";

    public string ExpectedPath =>
        Context.TryGetValue("ExpectedPath", out object? path) ? path?.ToString() ?? "" : "";

    public override bool IsRecoverable => true;

    public override string GetUserFriendlyMessage()
    {
        return $"Map '{MapId}' could not be found.";
    }
}

/// <summary>
///     Exception thrown when tileset data fails to load.
/// </summary>
public class TilesetLoadException : DataException
{
    public TilesetLoadException(string tilesetId, string message)
        : base("DATA_TILESET_LOAD_FAILED", message)
    {
        WithContext("TilesetId", tilesetId);
    }

    public TilesetLoadException(string tilesetId, string message, Exception innerException)
        : base("DATA_TILESET_LOAD_FAILED", message, innerException)
    {
        WithContext("TilesetId", tilesetId);
    }

    public string TilesetId =>
        Context.TryGetValue("TilesetId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // Map can't render without tileset

    public override string GetUserFriendlyMessage()
    {
        return "Failed to load map graphics. Please verify game files.";
    }
}

/// <summary>
///     Exception thrown when NPC definition data fails to load.
/// </summary>
public class NpcLoadException : DataException
{
    public NpcLoadException(string npcId, string message)
        : base("DATA_NPC_LOAD_FAILED", message)
    {
        WithContext("NpcId", npcId);
    }

    public NpcLoadException(string npcId, string message, Exception innerException)
        : base("DATA_NPC_LOAD_FAILED", message, innerException)
    {
        WithContext("NpcId", npcId);
    }

    public string NpcId => Context.TryGetValue("NpcId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Map can load without this NPC

    public override string GetUserFriendlyMessage()
    {
        return "Failed to load some NPCs. Gameplay may be affected.";
    }
}

/// <summary>
///     Exception thrown when trainer definition data fails to load.
/// </summary>
public class TrainerLoadException : DataException
{
    public TrainerLoadException(string trainerId, string message)
        : base("DATA_TRAINER_LOAD_FAILED", message)
    {
        WithContext("TrainerId", trainerId);
    }

    public TrainerLoadException(string trainerId, string message, Exception innerException)
        : base("DATA_TRAINER_LOAD_FAILED", message, innerException)
    {
        WithContext("TrainerId", trainerId);
    }

    public string TrainerId =>
        Context.TryGetValue("TrainerId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Map can load without this trainer

    public override string GetUserFriendlyMessage()
    {
        return "Failed to load some trainers. Gameplay may be affected.";
    }
}

/// <summary>
///     Exception thrown when JSON parsing fails for game data.
/// </summary>
public class DataParsingException : DataException
{
    public DataParsingException(string filePath, string message)
        : base("DATA_PARSING_FAILED", message)
    {
        WithContext("FilePath", filePath);
    }

    public DataParsingException(string filePath, string message, Exception innerException)
        : base("DATA_PARSING_FAILED", message, innerException)
    {
        WithContext("FilePath", filePath);
    }

    public string FilePath =>
        Context.TryGetValue("FilePath", out object? path) ? path?.ToString() ?? "" : "";

    public override bool IsRecoverable => false;

    public override string GetUserFriendlyMessage()
    {
        return "Failed to parse game data. The file may be corrupted.";
    }
}

/// <summary>
///     Exception thrown when data validation fails.
/// </summary>
public class DataValidationException : DataException
{
    public DataValidationException(string entityType, string entityId, string validationMessage)
        : base("DATA_VALIDATION_FAILED", validationMessage)
    {
        WithContext("EntityType", entityType).WithContext("EntityId", entityId);
    }

    public string EntityType =>
        Context.TryGetValue("EntityType", out object? type) ? type?.ToString() ?? "" : "";

    public string EntityId =>
        Context.TryGetValue("EntityId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Skip invalid data

    public override string GetUserFriendlyMessage()
    {
        return $"Invalid {EntityType} data detected. Some content may not load correctly.";
    }
}
