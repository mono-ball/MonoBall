using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Scripting.Services;

namespace PokeSharp.Core.ScriptingApi;

/// <summary>
///     Composed World API implementation that delegates to domain-specific services.
///     This follows the Interface Segregation Principle by splitting responsibilities
///     across multiple focused service implementations.
/// </summary>
public class WorldApi(
    PlayerApiService playerApi,
    MapApiService mapApi,
    NpcApiService npcApi,
    GameStateApiService gameStateApi
) : IWorldApi
{
    private readonly GameStateApiService _gameStateApi =
        gameStateApi ?? throw new ArgumentNullException(nameof(gameStateApi));

    private readonly MapApiService _mapApi =
        mapApi ?? throw new ArgumentNullException(nameof(mapApi));

    private readonly NpcApiService _npcApi =
        npcApi ?? throw new ArgumentNullException(nameof(npcApi));

    private readonly PlayerApiService _playerApi =
        playerApi ?? throw new ArgumentNullException(nameof(playerApi));

    // ============================================================================
    // IPlayerApi Implementation - Delegate to PlayerApiService
    // ============================================================================

    public string GetPlayerName()
    {
        return _playerApi.GetPlayerName();
    }

    public int GetMoney()
    {
        return _playerApi.GetMoney();
    }

    public void GiveMoney(int amount)
    {
        _playerApi.GiveMoney(amount);
    }

    public bool TakeMoney(int amount)
    {
        return _playerApi.TakeMoney(amount);
    }

    public bool HasMoney(int amount)
    {
        return _playerApi.HasMoney(amount);
    }

    public Point GetPlayerPosition()
    {
        return _playerApi.GetPlayerPosition();
    }

    public Direction GetPlayerFacing()
    {
        return _playerApi.GetPlayerFacing();
    }

    public void SetPlayerFacing(Direction direction)
    {
        _playerApi.SetPlayerFacing(direction);
    }

    public void SetPlayerMovementLocked(bool locked)
    {
        _playerApi.SetPlayerMovementLocked(locked);
    }

    public bool IsPlayerMovementLocked()
    {
        return _playerApi.IsPlayerMovementLocked();
    }

    // ============================================================================
    // IMapApi Implementation - Delegate to MapApiService
    // ============================================================================

    public bool IsPositionWalkable(int mapId, int x, int y)
    {
        return _mapApi.IsPositionWalkable(mapId, x, y);
    }

    public Entity[] GetEntitiesAt(int mapId, int x, int y)
    {
        return _mapApi.GetEntitiesAt(mapId, x, y);
    }

    public int GetCurrentMapId()
    {
        return _mapApi.GetCurrentMapId();
    }

    public void TransitionToMap(int mapId, int x, int y)
    {
        _mapApi.TransitionToMap(mapId, x, y);
    }

    public (int width, int height)? GetMapDimensions(int mapId)
    {
        return _mapApi.GetMapDimensions(mapId);
    }

    // ============================================================================
    // INPCApi Implementation - Delegate to NpcApiService
    // ============================================================================

    public void MoveNPC(Entity npc, Direction direction)
    {
        _npcApi.MoveNPC(npc, direction);
    }

    public void FaceDirection(Entity npc, Direction direction)
    {
        _npcApi.FaceDirection(npc, direction);
    }

    public void FaceEntity(Entity npc, Entity target)
    {
        _npcApi.FaceEntity(npc, target);
    }

    public Point GetNPCPosition(Entity npc)
    {
        return _npcApi.GetNPCPosition(npc);
    }

    public void SetNPCPath(Entity npc, Point[] waypoints, bool loop)
    {
        _npcApi.SetNPCPath(npc, waypoints, loop);
    }

    public bool IsNPCMoving(Entity npc)
    {
        return _npcApi.IsNPCMoving(npc);
    }

    public void StopNPC(Entity npc)
    {
        _npcApi.StopNPC(npc);
    }

    public Point[]? GetNPCPath(Entity npc)
    {
        return _npcApi.GetNPCPath(npc);
    }

    public void ClearNPCPath(Entity npc)
    {
        _npcApi.ClearNPCPath(npc);
    }

    public void PauseNPCPath(Entity npc)
    {
        _npcApi.PauseNPCPath(npc);
    }

    public void ResumeNPCPath(Entity npc, float waitTime = 0f)
    {
        _npcApi.ResumeNPCPath(npc, waitTime);
    }

    // ============================================================================
    // IGameStateApi Implementation - Delegate to GameStateApiService
    // ============================================================================

    public bool GetFlag(string flagId)
    {
        return _gameStateApi.GetFlag(flagId);
    }

    public void SetFlag(string flagId, bool value)
    {
        _gameStateApi.SetFlag(flagId, value);
    }

    public bool FlagExists(string flagId)
    {
        return _gameStateApi.FlagExists(flagId);
    }

    public string? GetVariable(string key)
    {
        return _gameStateApi.GetVariable(key);
    }

    public void SetVariable(string key, string value)
    {
        _gameStateApi.SetVariable(key, value);
    }

    public bool VariableExists(string key)
    {
        return _gameStateApi.VariableExists(key);
    }

    public void DeleteVariable(string key)
    {
        _gameStateApi.DeleteVariable(key);
    }

    public IEnumerable<string> GetActiveFlags()
    {
        return _gameStateApi.GetActiveFlags();
    }

    public IEnumerable<string> GetVariableKeys()
    {
        return _gameStateApi.GetVariableKeys();
    }
}