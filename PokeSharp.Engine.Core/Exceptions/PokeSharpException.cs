using System.Diagnostics.CodeAnalysis;

namespace PokeSharp.Engine.Core.Exceptions;

/// <summary>
///     Base exception for all PokeSharp-specific exceptions.
///     Provides standardized error codes, context data, and logging support.
/// </summary>
public abstract class PokeSharpException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PokeSharpException" /> class.
    /// </summary>
    /// <param name="errorCode">The error code identifying the exception type.</param>
    /// <param name="message">The error message.</param>
    protected PokeSharpException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PokeSharpException" /> class with an inner exception.
    /// </summary>
    /// <param name="errorCode">The error code identifying the exception type.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    protected PokeSharpException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    ///     Gets the error code that identifies the specific exception type.
    ///     Format: DOMAIN_CATEGORY_SPECIFIC (e.g., "DATA_MAP_NOT_FOUND")
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    ///     Gets the context data associated with this exception.
    ///     Stores additional diagnostic information (file paths, entity IDs, etc.)
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    ///     Gets the timestamp when this exception was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    ///     Determines if this exception is recoverable.
    ///     Override this in derived classes to indicate if the game can continue.
    /// </summary>
    public virtual bool IsRecoverable => false;

    /// <summary>
    ///     Adds context information to the exception.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This exception instance for method chaining.</returns>
    public PokeSharpException WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple context entries to the exception.
    /// </summary>
    /// <param name="contextData">Dictionary of context data to add.</param>
    /// <returns>This exception instance for method chaining.</returns>
    public PokeSharpException WithContext(IDictionary<string, object> contextData)
    {
        foreach (KeyValuePair<string, object> kvp in contextData)
        {
            Context[kvp.Key] = kvp.Value;
        }

        return this;
    }

    /// <summary>
    ///     Gets a formatted string representation of the exception with context.
    /// </summary>
    /// <returns>Formatted exception details.</returns>
    public override string ToString()
    {
        string details = $"[{ErrorCode}] {Message}";

        if (Context.Count > 0)
        {
            details += "\nContext:";
            foreach (KeyValuePair<string, object> kvp in Context)
            {
                details += $"\n  {kvp.Key}: {kvp.Value}";
            }
        }

        if (InnerException != null)
        {
            details += $"\nInner Exception: {InnerException.Message}";
        }

        return details;
    }

    /// <summary>
    ///     Gets a user-friendly error message suitable for displaying to players.
    ///     Override this in derived classes to provide domain-specific messages.
    /// </summary>
    /// <returns>User-friendly error message.</returns>
    public virtual string GetUserFriendlyMessage()
    {
        return "An error occurred while running the game. Please check the logs for details.";
    }

    /// <summary>
    ///     Tries to get a context value by key.
    /// </summary>
    /// <typeparam name="T">The expected type of the context value.</typeparam>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value if found and type matches.</param>
    /// <returns>True if value was found and type matches, false otherwise.</returns>
    public bool TryGetContext<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (Context.TryGetValue(key, out object? obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}
