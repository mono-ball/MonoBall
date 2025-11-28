using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Data.Entities;

namespace PokeSharp.Game.Data.Services;

/// <summary>
///     Service for querying NPC and Trainer definitions with caching.
///     Provides O(1) lookups for hot paths while maintaining EF Core query capabilities.
/// </summary>
public class NpcDefinitionService
{
    private readonly GameDataContext _context;
    private readonly ILogger<NpcDefinitionService> _logger;

    // Cache for O(1) lookups (hot paths like entity spawning)
    private readonly ConcurrentDictionary<string, NpcDefinition> _npcCache = new();
    private readonly ConcurrentDictionary<string, TrainerDefinition> _trainerCache = new();

    public NpcDefinitionService(GameDataContext context, ILogger<NpcDefinitionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Statistics

    /// <summary>
    ///     Get statistics about loaded data.
    /// </summary>
    public async Task<DataStatistics> GetStatisticsAsync()
    {
        var stats = new DataStatistics
        {
            TotalNpcs = await _context.Npcs.CountAsync(),
            TotalTrainers = await _context.Trainers.CountAsync(),
            NpcsCached = _npcCache.Count,
            TrainersCached = _trainerCache.Count,
        };

        return stats;
    }

    #endregion

    #region NPC Queries

    /// <summary>
    ///     Get NPC definition by ID (O(1) cached).
    /// </summary>
    public NpcDefinition? GetNpc(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            return null;
        }

        // Check cache first
        if (_npcCache.TryGetValue(npcId, out NpcDefinition? cached))
        {
            return cached;
        }

        // Query database
        NpcDefinition? npc = _context.Npcs.Find(npcId);

        // Cache for next time
        if (npc != null)
        {
            _npcCache[npcId] = npc;
            _logger.LogNpcCached(npcId);
        }

        return npc;
    }

    /// <summary>
    ///     Get all NPCs of a specific type.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<NpcDefinition>> GetNpcsByTypeAsync(string npcType)
    {
        return await _context.Npcs.AsNoTracking().Where(n => n.NpcType == npcType).ToListAsync();
    }

    /// <summary>
    ///     Get all NPCs from a specific mod.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<NpcDefinition>> GetNpcsByModAsync(string modId)
    {
        return await _context.Npcs.AsNoTracking().Where(n => n.SourceMod == modId).ToListAsync();
    }

    /// <summary>
    ///     Check if NPC definition exists.
    /// </summary>
    public bool HasNpc(string npcId)
    {
        return GetNpc(npcId) != null;
    }

    #endregion

    #region Trainer Queries

    /// <summary>
    ///     Get trainer definition by ID (O(1) cached).
    /// </summary>
    public TrainerDefinition? GetTrainer(string trainerId)
    {
        if (string.IsNullOrWhiteSpace(trainerId))
        {
            return null;
        }

        // Check cache first
        if (_trainerCache.TryGetValue(trainerId, out TrainerDefinition? cached))
        {
            return cached;
        }

        // Query database
        TrainerDefinition? trainer = _context.Trainers.Find(trainerId);

        // Cache for next time
        if (trainer != null)
        {
            _trainerCache[trainerId] = trainer;
            _logger.LogTrainerCached(trainerId);
        }

        return trainer;
    }

    /// <summary>
    ///     Get all trainers of a specific class (e.g., "gym_leader", "youngster").
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<TrainerDefinition>> GetTrainersByClassAsync(string trainerClass)
    {
        return await _context
            .Trainers.AsNoTracking()
            .Where(t => t.TrainerClass == trainerClass)
            .ToListAsync();
    }

    /// <summary>
    ///     Get all trainers from a specific mod.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<TrainerDefinition>> GetTrainersByModAsync(string modId)
    {
        return await _context
            .Trainers.AsNoTracking()
            .Where(t => t.SourceMod == modId)
            .ToListAsync();
    }

    /// <summary>
    ///     Check if trainer definition exists.
    /// </summary>
    public bool HasTrainer(string trainerId)
    {
        return GetTrainer(trainerId) != null;
    }

    #endregion
}

/// <summary>
///     Statistics about loaded game data.
/// </summary>
public record DataStatistics
{
    public int TotalNpcs { get; init; }
    public int TotalTrainers { get; init; }
    public int NpcsCached { get; init; }
    public int TrainersCached { get; init; }
}
