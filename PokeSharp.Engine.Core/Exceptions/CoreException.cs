namespace PokeSharp.Engine.Core.Exceptions;

/// <summary>
///     Base exception for core engine errors (ECS, templates, systems management).
/// </summary>
public abstract class CoreException : PokeSharpException
{
    protected CoreException(string errorCode, string message)
        : base(errorCode, message) { }

    protected CoreException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException) { }

    public override string GetUserFriendlyMessage()
    {
        return "A core engine error occurred. The game may need to restart.";
    }
}

/// <summary>
///     Exception thrown when ECS operations fail.
/// </summary>
public class EcsException : CoreException
{
    public EcsException(string operation, string message)
        : base("CORE_ECS_ERROR", message)
    {
        WithContext("Operation", operation);
    }

    public EcsException(string operation, string message, Exception innerException)
        : base("CORE_ECS_ERROR", message, innerException)
    {
        WithContext("Operation", operation);
    }

    public string Operation =>
        Context.TryGetValue("Operation", out object? op) ? op?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // ECS errors are critical

    public override string GetUserFriendlyMessage()
    {
        return "Entity system error. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when entity templates fail to load or compile.
/// </summary>
public class TemplateException : CoreException
{
    public TemplateException(string templateName, string message)
        : base("CORE_TEMPLATE_ERROR", message)
    {
        WithContext("TemplateName", templateName);
    }

    public TemplateException(string templateName, string message, Exception innerException)
        : base("CORE_TEMPLATE_ERROR", message, innerException)
    {
        WithContext("TemplateName", templateName);
    }

    public string TemplateName =>
        Context.TryGetValue("TemplateName", out object? name) ? name?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can skip template-based entities

    public override string GetUserFriendlyMessage()
    {
        return $"Template '{TemplateName}' failed to load. Some entities may not spawn.";
    }
}

/// <summary>
///     Exception thrown when system registration or management fails.
/// </summary>
public class SystemManagementException : CoreException
{
    public SystemManagementException(string systemName, string message)
        : base("CORE_SYSTEM_MANAGEMENT_ERROR", message)
    {
        WithContext("SystemName", systemName);
    }

    public SystemManagementException(string systemName, string message, Exception innerException)
        : base("CORE_SYSTEM_MANAGEMENT_ERROR", message, innerException)
    {
        WithContext("SystemName", systemName);
    }

    public string SystemName =>
        Context.TryGetValue("SystemName", out object? name) ? name?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // System failures are critical

    public override string GetUserFriendlyMessage()
    {
        return "System initialization error. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when component registration fails.
/// </summary>
public class ComponentRegistrationException : CoreException
{
    public ComponentRegistrationException(string componentType, string message)
        : base("CORE_COMPONENT_REGISTRATION_ERROR", message)
    {
        WithContext("ComponentType", componentType);
    }

    public ComponentRegistrationException(
        string componentType,
        string message,
        Exception innerException
    )
        : base("CORE_COMPONENT_REGISTRATION_ERROR", message, innerException)
    {
        WithContext("ComponentType", componentType);
    }

    public string ComponentType =>
        Context.TryGetValue("ComponentType", out object? type) ? type?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // Component registration failures are critical

    public override string GetUserFriendlyMessage()
    {
        return "Component system error. Please restart the game.";
    }
}

/// <summary>
///     Exception thrown when event bus operations fail.
/// </summary>
public class EventBusException : CoreException
{
    public EventBusException(string eventType, string message)
        : base("CORE_EVENT_BUS_ERROR", message)
    {
        WithContext("EventType", eventType);
    }

    public EventBusException(string eventType, string message, Exception innerException)
        : base("CORE_EVENT_BUS_ERROR", message, innerException)
    {
        WithContext("EventType", eventType);
    }

    public string EventType =>
        Context.TryGetValue("EventType", out object? type) ? type?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Event failures shouldn't crash the game

    public override string GetUserFriendlyMessage()
    {
        return "Event system error. Some game events may not trigger.";
    }
}

/// <summary>
///     Exception thrown when modding operations fail.
/// </summary>
public class ModdingException : CoreException
{
    public ModdingException(string modName, string message)
        : base("CORE_MODDING_ERROR", message)
    {
        WithContext("ModName", modName);
    }

    public ModdingException(string modName, string message, Exception innerException)
        : base("CORE_MODDING_ERROR", message, innerException)
    {
        WithContext("ModName", modName);
    }

    public string ModName =>
        Context.TryGetValue("ModName", out object? name) ? name?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can continue without mod

    public override string GetUserFriendlyMessage()
    {
        return $"Mod '{ModName}' failed to load. The game will continue without it.";
    }
}
