using MonoBallFramework.Game.Engine.Core.Exceptions;

namespace MonoBallFramework.Game.GameSystems.Exceptions;

/// <summary>
///     Base exception for all game system errors (movement, tiles, NPCs, etc.)
/// </summary>
public abstract class SystemException : MonoBallFrameworkException
{
    protected SystemException(string errorCode, string message)
        : base(errorCode, message)
    {
    }

    protected SystemException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException)
    {
    }

    public override string GetUserFriendlyMessage()
    {
        return "A game system error occurred. Gameplay may be affected.";
    }
}

/// <summary>
///     Exception thrown when movement system encounters an error.
/// </summary>
public class MovementException : SystemException
{
    public MovementException(int entityId, string message)
        : base("SYSTEM_MOVEMENT_ERROR", message)
    {
        WithContext("EntityId", entityId);
    }

    public MovementException(int entityId, string message, Exception innerException)
        : base("SYSTEM_MOVEMENT_ERROR", message, innerException)
    {
        WithContext("EntityId", entityId);
    }

    public int EntityId => Context.TryGetValue("EntityId", out object? id) && id is int i ? i : 0;

    public override bool IsRecoverable => true; // Can skip movement update

    public override string GetUserFriendlyMessage()
    {
        return "Movement error occurred. Character movement may be affected.";
    }
}

/// <summary>
///     Exception thrown when collision detection fails.
/// </summary>
public class CollisionException : SystemException
{
    public CollisionException(int entityId, string message)
        : base("SYSTEM_COLLISION_ERROR", message)
    {
        WithContext("EntityId", entityId);
    }

    public CollisionException(int entityId, string message, Exception innerException)
        : base("SYSTEM_COLLISION_ERROR", message, innerException)
    {
        WithContext("EntityId", entityId);
    }

    public int EntityId => Context.TryGetValue("EntityId", out object? id) && id is int i ? i : 0;

    public override bool IsRecoverable => true; // Can allow movement without collision

    public override string GetUserFriendlyMessage()
    {
        return "Collision detection error. Characters may pass through objects.";
    }
}

/// <summary>
///     Exception thrown when tile animation system fails.
/// </summary>
public class TileAnimationException : SystemException
{
    public TileAnimationException(int tileId, string message)
        : base("SYSTEM_TILE_ANIMATION_ERROR", message)
    {
        WithContext("TileId", tileId);
    }

    public TileAnimationException(int tileId, string message, Exception innerException)
        : base("SYSTEM_TILE_ANIMATION_ERROR", message, innerException)
    {
        WithContext("TileId", tileId);
    }

    public int TileId => Context.TryGetValue("TileId", out object? id) && id is int i ? i : 0;

    public override bool IsRecoverable => true; // Static tiles still work

    public override string GetUserFriendlyMessage()
    {
        return "Tile animation error. Some animated tiles may appear static.";
    }
}

/// <summary>
///     Exception thrown when spatial hash or spatial query fails.
/// </summary>
public class SpatialHashException : SystemException
{
    public SpatialHashException(string operation, string message)
        : base("SYSTEM_SPATIAL_HASH_ERROR", message)
    {
        WithContext("Operation", operation);
    }

    public SpatialHashException(string operation, string message, Exception innerException)
        : base("SYSTEM_SPATIAL_HASH_ERROR", message, innerException)
    {
        WithContext("Operation", operation);
    }

    public string Operation =>
        Context.TryGetValue("Operation", out object? op) ? op!.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can fallback to brute force queries

    public override string GetUserFriendlyMessage()
    {
        return "Spatial query error. Performance may be degraded.";
    }
}

/// <summary>
///     Exception thrown when map streaming encounters an error.
/// </summary>
public class MapStreamingException : SystemException
{
    public MapStreamingException(string mapId, string message)
        : base("SYSTEM_MAP_STREAMING_ERROR", message)
    {
        WithContext("MapId", mapId);
    }

    public MapStreamingException(string mapId, string message, Exception innerException)
        : base("SYSTEM_MAP_STREAMING_ERROR", message, innerException)
    {
        WithContext("MapId", mapId);
    }

    public string MapId => Context.TryGetValue("MapId", out object? id) ? id!.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Current map still works

    public override string GetUserFriendlyMessage()
    {
        return "Map streaming error. Adjacent maps may not load.";
    }
}

/// <summary>
///     Exception thrown when NPC behavior script execution fails.
/// </summary>
public class NpcBehaviorException : SystemException
{
    public NpcBehaviorException(int npcEntityId, string scriptName, string message)
        : base("SYSTEM_NPC_BEHAVIOR_ERROR", message)
    {
        WithContext("NpcEntityId", npcEntityId).WithContext("ScriptName", scriptName);
    }

    public NpcBehaviorException(
        int npcEntityId,
        string scriptName,
        string message,
        Exception innerException
    )
        : base("SYSTEM_NPC_BEHAVIOR_ERROR", message, innerException)
    {
        WithContext("NpcEntityId", npcEntityId).WithContext("ScriptName", scriptName);
    }

    public int NpcEntityId =>
        Context.TryGetValue("NpcEntityId", out object? id) && id is int i ? i : 0;

    public string ScriptName =>
        Context.TryGetValue("ScriptName", out object? name) ? name!.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // NPC can use default behavior

    public override string GetUserFriendlyMessage()
    {
        return "NPC behavior error. Some NPCs may not behave correctly.";
    }
}
