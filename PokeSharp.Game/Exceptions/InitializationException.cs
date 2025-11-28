using PokeSharp.Engine.Core.Exceptions;

namespace PokeSharp.Game.Exceptions;

/// <summary>
///     Base exception for game initialization errors.
/// </summary>
public abstract class InitializationException : PokeSharpException
{
    protected InitializationException(string errorCode, string message)
        : base(errorCode, message) { }

    protected InitializationException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException) { }

    public override string GetUserFriendlyMessage()
    {
        return "Game initialization failed. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when configuration loading fails.
/// </summary>
public class ConfigurationException : InitializationException
{
    public ConfigurationException(string configSection, string message)
        : base("INIT_CONFIGURATION_ERROR", message)
    {
        WithContext("ConfigSection", configSection);
    }

    public ConfigurationException(string configSection, string message, Exception innerException)
        : base("INIT_CONFIGURATION_ERROR", message, innerException)
    {
        WithContext("ConfigSection", configSection);
    }

    public string ConfigSection =>
        Context.TryGetValue("ConfigSection", out object? section) ? section?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // Config errors prevent startup

    public override string GetUserFriendlyMessage()
    {
        return $"Configuration error in '{ConfigSection}'. Please check your settings.";
    }
}

/// <summary>
///     Exception thrown when dependency injection configuration fails.
/// </summary>
public class DependencyInjectionException : InitializationException
{
    public DependencyInjectionException(string serviceType, string message)
        : base("INIT_DI_ERROR", message)
    {
        WithContext("ServiceType", serviceType);
    }

    public DependencyInjectionException(
        string serviceType,
        string message,
        Exception innerException
    )
        : base("INIT_DI_ERROR", message, innerException)
    {
        WithContext("ServiceType", serviceType);
    }

    public string ServiceType =>
        Context.TryGetValue("ServiceType", out object? type) ? type?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // DI errors prevent startup

    public override string GetUserFriendlyMessage()
    {
        return "Service initialization error. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when game initialization pipeline fails.
/// </summary>
public class InitializationPipelineException : InitializationException
{
    public InitializationPipelineException(string stepName, string message)
        : base("INIT_PIPELINE_ERROR", message)
    {
        WithContext("StepName", stepName);
    }

    public InitializationPipelineException(
        string stepName,
        string message,
        Exception innerException
    )
        : base("INIT_PIPELINE_ERROR", message, innerException)
    {
        WithContext("StepName", stepName);
    }

    public string StepName =>
        Context.TryGetValue("StepName", out object? step) ? step?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // Pipeline failures prevent startup

    public override string GetUserFriendlyMessage()
    {
        return $"Initialization failed at step '{StepName}'. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when player initialization fails.
/// </summary>
public class PlayerInitializationException : InitializationException
{
    public PlayerInitializationException(string message)
        : base("INIT_PLAYER_ERROR", message) { }

    public PlayerInitializationException(string message, Exception innerException)
        : base("INIT_PLAYER_ERROR", message, innerException) { }

    public override bool IsRecoverable => false; // Can't play without player

    public override string GetUserFriendlyMessage()
    {
        return "Failed to initialize player. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when initial map loading fails during startup.
/// </summary>
public class InitialMapLoadException : InitializationException
{
    public InitialMapLoadException(string mapId, string message)
        : base("INIT_MAP_LOAD_ERROR", message)
    {
        WithContext("MapId", mapId);
    }

    public InitialMapLoadException(string mapId, string message, Exception innerException)
        : base("INIT_MAP_LOAD_ERROR", message, innerException)
    {
        WithContext("MapId", mapId);
    }

    public string MapId => Context.TryGetValue("MapId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // Need initial map to start

    public override string GetUserFriendlyMessage()
    {
        return $"Failed to load starting map '{MapId}'. Please verify game files.";
    }
}

/// <summary>
///     Exception thrown when asset manager initialization fails.
/// </summary>
public class AssetManagerInitializationException : InitializationException
{
    public AssetManagerInitializationException(string message)
        : base("INIT_ASSET_MANAGER_ERROR", message) { }

    public AssetManagerInitializationException(string message, Exception innerException)
        : base("INIT_ASSET_MANAGER_ERROR", message, innerException) { }

    public override bool IsRecoverable => false; // Can't render without assets

    public override string GetUserFriendlyMessage()
    {
        return "Failed to initialize asset manager. Please verify game files.";
    }
}
