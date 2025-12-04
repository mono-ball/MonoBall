using Microsoft.Extensions.Logging;
using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Game state management service implementation.
/// </summary>
public class GameStateApiService(ILogger<GameStateApiService> logger) : IGameStateApi
{
    private readonly Dictionary<string, bool> _flags = new();

    private readonly ILogger<GameStateApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly Dictionary<string, string> _variables = new();

    public bool GetFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        return _flags.TryGetValue(flagId, out bool value) && value;
    }

    public void SetFlag(string flagId, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            throw new ArgumentException("Flag ID cannot be null or empty", nameof(flagId));
        }

        _flags[flagId] = value;
        _logger.LogDebug("Flag {FlagId} set to {Value}", flagId, value);
    }

    public bool FlagExists(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        return _flags.ContainsKey(flagId);
    }

    public string? GetVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return _variables.TryGetValue(key, out string? value) ? value : null;
    }

    public void SetVariable(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Variable key cannot be null or empty", nameof(key));
        }

        _variables[key] = value;
        _logger.LogDebug("Variable {Key} set to {Value}", key, value);
    }

    public bool VariableExists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _variables.ContainsKey(key);
    }

    public void DeleteVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _variables.Remove(key);
        _logger.LogDebug("Variable {Key} deleted", key);
    }

    public IEnumerable<string> GetActiveFlags()
    {
        return _flags.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }

    public IEnumerable<string> GetVariableKeys()
    {
        return _variables.Keys;
    }

    public float Random()
    {
        return (float)System.Random.Shared.NextDouble();
    }

    public int RandomRange(int min, int max)
    {
        if (min >= max)
        {
            throw new ArgumentException("min must be less than max", nameof(min));
        }

        return System.Random.Shared.Next(min, max);
    }
}
