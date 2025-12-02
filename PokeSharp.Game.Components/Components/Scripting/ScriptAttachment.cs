namespace PokeSharp.Game.Components.Scripting;

/// <summary>
///     Component for attaching scripts to entities with composition support.
///     Multiple ScriptAttachment components can be added to the same entity for multi-script behavior.
/// </summary>
/// <remarks>
///     <para>
///         This component enables composition-based scripting where multiple scripts can:
///         - Attach to the same entity/tile
///         - Execute in priority order
///         - Receive events independently
///         - Be added/removed dynamically
///     </para>
///     <para>
///         Example usage:
///         <code>
///         // Ice tile with encounter rate and warp
///         entity.Add(new ScriptAttachment("tiles/ice_slide.csx", priority: 10));
///         entity.Add(new ScriptAttachment("tiles/wild_encounter.csx", priority: 5));
///         entity.Add(new ScriptAttachment("tiles/warp.csx", priority: 1));
///         </code>
///     </para>
/// </remarks>
public struct ScriptAttachment
{
    /// <summary>
    ///     Gets the path to the script file (.csx).
    /// </summary>
    /// <remarks>
    ///     Path is relative to the scripts directory.
    ///     Example: "tiles/ice_slide.csx", "entities/npc_behavior.csx"
    /// </remarks>
    public string ScriptPath { get; init; }

    /// <summary>
    ///     Gets or sets the loaded script instance.
    ///     Populated by ScriptAttachmentSystem after loading.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is set during script initialization and should not be modified manually.
    ///         The system manages the lifecycle of this instance.
    ///     </para>
    ///     <para>
    ///         Type: TypeScriptBase (stored as object to avoid circular dependency)
    ///     </para>
    /// </remarks>
    public object? ScriptInstance { get; set; }

    /// <summary>
    ///     Gets the execution priority for this script (higher = executes first).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When multiple scripts are attached to the same entity:
    ///         - Higher priority scripts execute first
    ///         - Equal priority scripts execute in registration order
    ///         - Priority affects both event handling and tick execution
    ///     </para>
    ///     <para>
    ///         Example priority scheme:
    ///         - 100: Critical behaviors (blocking, warps)
    ///         - 50: Normal behaviors (ice, conveyors)
    ///         - 10: Cosmetic behaviors (particles, sounds)
    ///         - 0: Default priority
    ///     </para>
    /// </remarks>
    public int Priority { get; init; }

    /// <summary>
    ///     Gets whether the script has been initialized.
    ///     Used by ScriptAttachmentSystem to track initialization state.
    /// </summary>
    internal bool IsInitialized { get; set; }

    /// <summary>
    ///     Gets whether the script is currently active.
    ///     Scripts can be temporarily disabled without removing them.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Creates a new script attachment with the specified path and priority.
    /// </summary>
    /// <param name="scriptPath">Path to the script file (relative to scripts directory)</param>
    /// <param name="priority">Execution priority (higher = executes first). Default: 0</param>
    public ScriptAttachment(string scriptPath, int priority = 0)
    {
        ScriptPath = scriptPath;
        Priority = priority;
        ScriptInstance = null;
        IsInitialized = false;
        IsActive = true;
    }

    /// <summary>
    ///     Creates a new script attachment with an already-loaded script instance.
    /// </summary>
    /// <param name="scriptPath">Path to the script file</param>
    /// <param name="scriptInstance">Pre-loaded script instance (must be TypeScriptBase)</param>
    /// <param name="priority">Execution priority (higher = executes first)</param>
    public ScriptAttachment(string scriptPath, object scriptInstance, int priority = 0)
    {
        ScriptPath = scriptPath;
        ScriptInstance = scriptInstance;
        Priority = priority;
        IsInitialized = false;
        IsActive = true;
    }
}
