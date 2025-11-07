using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;

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
    GameStateApiService gameStateApi) : IWorldApi
{
    private readonly PlayerApiService _playerApi = playerApi ?? throw new ArgumentNullException(nameof(playerApi));
    private readonly MapApiService _mapApi = mapApi ?? throw new ArgumentNullException(nameof(mapApi));
    private readonly NpcApiService _npcApi = npcApi ?? throw new ArgumentNullException(nameof(npcApi));
    private readonly GameStateApiService _gameStateApi = gameStateApi ?? throw new ArgumentNullException(nameof(gameStateApi));

    // ============================================================================
    // IPlayerApi Implementation - Delegate to PlayerApiService
    // ============================================================================

    public string GetPlayerName() => _playerApi.GetPlayerName();

    public int GetMoney() => _playerApi.GetMoney();

    public void GiveMoney(int amount) => _playerApi.GiveMoney(amount);

    public bool TakeMoney(int amount) => _playerApi.TakeMoney(amount);

    public bool HasMoney(int amount) => _playerApi.HasMoney(amount);

    public Point GetPlayerPosition() => _playerApi.GetPlayerPosition();

    public Direction GetPlayerFacing() => _playerApi.GetPlayerFacing();

    public void SetPlayerFacing(Direction direction) => _playerApi.SetPlayerFacing(direction);

    public void SetPlayerMovementLocked(bool locked) => _playerApi.SetPlayerMovementLocked(locked);

    public bool IsPlayerMovementLocked() => _playerApi.IsPlayerMovementLocked();

    // ============================================================================
    // IMapApi Implementation - Delegate to MapApiService
    // ============================================================================

    public bool IsPositionWalkable(int mapId, int x, int y) =>
        _mapApi.IsPositionWalkable(mapId, x, y);

    public Entity[] GetEntitiesAt(int mapId, int x, int y) =>
        _mapApi.GetEntitiesAt(mapId, x, y);

    public int GetCurrentMapId() => _mapApi.GetCurrentMapId();

    public void TransitionToMap(int mapId, int x, int y) =>
        _mapApi.TransitionToMap(mapId, x, y);

    public (int width, int height)? GetMapDimensions(int mapId) =>
        _mapApi.GetMapDimensions(mapId);

    // ============================================================================
    // INPCApi Implementation - Delegate to NpcApiService
    // ============================================================================

    public void MoveNPC(Entity npc, Direction direction) =>
        _npcApi.MoveNPC(npc, direction);

    public void FaceDirection(Entity npc, Direction direction) =>
        _npcApi.FaceDirection(npc, direction);

    public void FaceEntity(Entity npc, Entity target) =>
        _npcApi.FaceEntity(npc, target);

    public Point GetNPCPosition(Entity npc) =>
        _npcApi.GetNPCPosition(npc);

    public void SetNPCPath(Entity npc, Point[] waypoints, bool loop) =>
        _npcApi.SetNPCPath(npc, waypoints, loop);

    public bool IsNPCMoving(Entity npc) =>
        _npcApi.IsNPCMoving(npc);

    public void StopNPC(Entity npc) =>
        _npcApi.StopNPC(npc);

    // ============================================================================
    // IGameStateApi Implementation - Delegate to GameStateApiService
    // ============================================================================

    public bool GetFlag(string flagId) => _gameStateApi.GetFlag(flagId);

    public void SetFlag(string flagId, bool value) => _gameStateApi.SetFlag(flagId, value);

    public bool FlagExists(string flagId) => _gameStateApi.FlagExists(flagId);

    public string? GetVariable(string key) => _gameStateApi.GetVariable(key);

    public void SetVariable(string key, string value) => _gameStateApi.SetVariable(key, value);

    public bool VariableExists(string key) => _gameStateApi.VariableExists(key);

    public void DeleteVariable(string key) => _gameStateApi.DeleteVariable(key);

    public IEnumerable<string> GetActiveFlags() => _gameStateApi.GetActiveFlags();

    public IEnumerable<string> GetVariableKeys() => _gameStateApi.GetVariableKeys();
}
