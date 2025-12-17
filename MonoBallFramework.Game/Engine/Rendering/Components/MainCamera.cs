namespace MonoBallFramework.Game.Engine.Rendering.Components;

/// <summary>
///     Tag component that marks the primary/active camera in the game world.
///     Used to decouple camera from Player component and enable multiple cameras.
/// </summary>
/// <remarks>
///     <para>
///         This tag component follows ECS best practices by separating concerns:
///         - Camera component: holds camera data (position, zoom, etc.)
///         - MainCamera tag: marks which camera is the active one
///         - Player component: separate concern from camera
///     </para>
///     <para>
///         Benefits:
///         - Can have cameras without players (security cams, cutscenes)
///         - Can have multiple cameras and switch between them
///         - Can easily query for the main camera: QueryCache.Get&lt;Camera, MainCamera&gt;()
///     </para>
/// </remarks>
public struct MainCamera
{
    // Tag component - no data needed
}
