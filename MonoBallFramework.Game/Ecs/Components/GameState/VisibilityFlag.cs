using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.GameState;

/// <summary>
///     Component that links an entity's visibility to a game flag.
///     When the referenced flag changes, the entity's Visible component
///     is added or removed based on the flag state.
/// </summary>
/// <remarks>
///     <para>
///         This enables flag-based NPC spawning similar to pokeemerald's FLAG_HIDE_* system.
///         NPCs can be configured in Tiled with a visibility flag that controls whether
///         they appear based on story progression.
///     </para>
///     <para>
///         <b>Usage Patterns:</b>
///         <list type="bullet">
///             <item>FLAG_HIDE_* pattern: Set <see cref="HideWhenTrue"/> = true (default)</item>
///             <item>FLAG_SHOW_* pattern: Set <see cref="HideWhenTrue"/> = false</item>
///         </list>
///     </para>
///     <para>
///         <b>Example Tiled Properties:</b>
///         <code>
///         visibilityFlag: "base:flag:hide/rival_oak_lab"
///         hideWhenFlagSet: true
///         </code>
///     </para>
/// </remarks>
public struct VisibilityFlag
{
    /// <summary>
    ///     The flag ID that controls this entity's visibility.
    /// </summary>
    public GameFlagId FlagId { get; set; }

    /// <summary>
    ///     When true, the entity is hidden when the flag is set (FLAG_HIDE_* pattern).
    ///     When false, the entity is shown when the flag is set (FLAG_SHOW_* pattern).
    ///     Default is true (hide when flag is set).
    /// </summary>
    public bool HideWhenTrue { get; set; }

    /// <summary>
    ///     Creates a visibility flag component with the FLAG_HIDE_* pattern.
    ///     Entity will be hidden when the flag is true.
    /// </summary>
    /// <param name="flagId">The flag ID controlling visibility.</param>
    public VisibilityFlag(GameFlagId flagId)
    {
        FlagId = flagId;
        HideWhenTrue = true;
    }

    /// <summary>
    ///     Creates a visibility flag component with configurable show/hide behavior.
    /// </summary>
    /// <param name="flagId">The flag ID controlling visibility.</param>
    /// <param name="hideWhenTrue">If true, hide entity when flag is set. If false, show entity when flag is set.</param>
    public VisibilityFlag(GameFlagId flagId, bool hideWhenTrue)
    {
        FlagId = flagId;
        HideWhenTrue = hideWhenTrue;
    }

    /// <summary>
    ///     Determines if the entity should be visible given the current flag state.
    /// </summary>
    /// <param name="flagValue">The current value of the flag.</param>
    /// <returns>True if the entity should have the Visible component.</returns>
    public readonly bool ShouldBeVisible(bool flagValue)
    {
        // HideWhenTrue=true:  flag=false -> visible, flag=true -> hidden
        // HideWhenTrue=false: flag=false -> hidden, flag=true -> visible
        return HideWhenTrue ? !flagValue : flagValue;
    }
}
