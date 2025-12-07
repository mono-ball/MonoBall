namespace MonoBallFramework.Game.Ecs.Components.GameState;

/// <summary>
///     Tag component identifying the singleton game state entity.
///     Used for entity queries to find the game state entity.
/// </summary>
/// <remarks>
///     The game state entity holds global game data such as flags and variables.
///     There should only be one entity with this component in the world.
/// </remarks>
public struct GameState;
