namespace PokeSharp.Scripting.HotReload.Cache;

/// <summary>
///     Represents a single versioned cache entry for a script type with rollback support.
///     Maintains current version, type, lazy instance, and link to previous version.
/// </summary>
public class ScriptCacheEntry
{
    private readonly object _instanceLock = new();
    private object? _instance;

    public ScriptCacheEntry(int version, Type scriptType)
    {
        Version = version;
        ScriptType = scriptType ?? throw new ArgumentNullException(nameof(scriptType));
        LastUpdated = DateTime.UtcNow;
        _instance = null; // Lazy - will be created on first GetOrCreateInstance()
    }

    /// <summary>
    ///     Version number for this cache entry. Incremented on each successful compilation.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    ///     Compiled Type for this script version.
    /// </summary>
    public Type ScriptType { get; set; }

    /// <summary>
    ///     Lazy-initialized singleton instance. Created only when first requested via GetOrCreateInstance().
    /// </summary>
    public object? Instance
    {
        get
        {
            lock (_instanceLock)
            {
                return _instance;
            }
        }
        set
        {
            lock (_instanceLock)
            {
                _instance = value;
            }
        }
    }

    /// <summary>
    ///     Timestamp when this version was created/updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    ///     Link to previous version for rollback support. Null if this is the first version.
    /// </summary>
    public ScriptCacheEntry? PreviousVersion { get; set; }

    /// <summary>
    ///     Check if instance has been created (not lazy anymore).
    /// </summary>
    public bool IsInstantiated
    {
        get
        {
            lock (_instanceLock)
            {
                return _instance != null;
            }
        }
    }

    /// <summary>
    ///     Create or retrieve the singleton instance for this cache entry.
    ///     Thread-safe lazy instantiation.
    /// </summary>
    public object GetOrCreateInstance()
    {
        lock (_instanceLock)
        {
            if (_instance == null)
                try
                {
                    _instance =
                        Activator.CreateInstance(ScriptType)
                        ?? throw new InvalidOperationException(
                            $"Failed to create instance of {ScriptType.Name}"
                        );
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to instantiate script type {ScriptType.Name}: {ex.Message}",
                        ex
                    );
                }

            return _instance;
        }
    }

    /// <summary>
    ///     Clear the instance to allow re-creation (useful for testing or forced refresh).
    /// </summary>
    public void ClearInstance()
    {
        lock (_instanceLock)
        {
            _instance = null;
        }
    }

    /// <summary>
    ///     Clone this entry with a new version number (for creating backups).
    /// </summary>
    public ScriptCacheEntry Clone(int newVersion)
    {
        lock (_instanceLock)
        {
            return new ScriptCacheEntry(newVersion, ScriptType)
            {
                Instance = _instance, // Share same instance reference
                LastUpdated = LastUpdated,
                PreviousVersion = PreviousVersion,
            };
        }
    }
}
