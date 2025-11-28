using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Represents a single frame of UI state in the immediate mode system.
///     Tracks which components were rendered this frame for input handling.
/// </summary>
public class UIFrame
{
    /// <summary>Frame number (increments each frame)</summary>
    public int FrameNumber { get; set; }

    /// <summary>Components rendered this frame (by ID)</summary>
    public Dictionary<string, ComponentFrameState> Components { get; } = new();

    /// <summary>ID of the component that has focus</summary>
    public string? FocusedComponentId { get; set; }

    /// <summary>ID of the component under the mouse</summary>
    public string? HoveredComponentId { get; set; }

    /// <summary>ID of the component being pressed</summary>
    public string? PressedComponentId { get; set; }

    /// <summary>ID of the component that has captured input (receives all input events)</summary>
    public string? CapturedComponentId { get; set; }

    /// <summary>
    ///     Clears frame state for a new frame.
    /// </summary>
    public void BeginFrame()
    {
        Components.Clear();
        FrameNumber++;
        // Note: CapturedComponentId persists across frames until released
    }
}

/// <summary>
///     State for a component in a single frame.
/// </summary>
public class ComponentFrameState
{
    public string Id { get; init; } = string.Empty;
    public string? ParentId { get; set; }
    public LayoutRect Rect { get; set; }
    public bool IsInteractive { get; set; }
    public bool IsVisible { get; set; } = true;
    public int ZOrder { get; set; }
}
