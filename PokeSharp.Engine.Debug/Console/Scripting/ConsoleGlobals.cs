using System.Text;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;

namespace PokeSharp.Engine.Debug.Console.Scripting;

/// <summary>
///     Global variables and helper methods available to console scripts.
///     Matches the ScriptContext pattern used by NPC behavior scripts for consistency.
/// </summary>
public class ConsoleGlobals
{
    private readonly IScriptingApiProvider _apis;

    /// <summary>
    ///     Initializes a new instance of ConsoleGlobals.
    /// </summary>
    public ConsoleGlobals(
        IScriptingApiProvider apis,
        World world,
        SystemManager systems,
        GraphicsDevice graphics,
        ILogger logger
    )
    {
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Systems = systems ?? throw new ArgumentNullException(nameof(systems));
        Graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Core Properties

    /// <summary>
    ///     The ECS World containing all entities.
    /// </summary>
    public World World { get; }

    /// <summary>
    ///     The SystemManager for accessing game systems.
    /// </summary>
    public SystemManager Systems { get; }

    /// <summary>
    ///     GraphicsDevice for accessing rendering information.
    /// </summary>
    public GraphicsDevice Graphics { get; }

    /// <summary>
    ///     Logger for script debugging.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    ///     Output action for printing to console.
    /// </summary>
    public Action<string>? OutputAction { get; set; }

    /// <summary>
    ///     Script arguments passed via 'load script.csx arg1 arg2 ...'
    ///     Access in scripts as: args[0], args[1], etc.
    /// </summary>
    public string[] Args { get; set; } = Array.Empty<string>();

    #endregion

    #region API Services (Matching ScriptContext pattern)

    /// <summary>
    ///     Gets the Player API service - same pattern as ScriptContext.Player in NPC behaviors.
    /// </summary>
    public PlayerApiService Player => _apis.Player;

    /// <summary>
    ///     Gets the NPC API service - same pattern as ScriptContext.Npc in NPC behaviors.
    /// </summary>
    public NpcApiService Npc => _apis.Npc;

    /// <summary>
    ///     Gets the Map API service - same pattern as ScriptContext.Map in NPC behaviors.
    /// </summary>
    public MapApiService Map => _apis.Map;

    /// <summary>
    ///     Gets the Game State API service - same pattern as ScriptContext.GameState in NPC behaviors.
    /// </summary>
    public GameStateApiService GameState => _apis.GameState;

    /// <summary>
    ///     Gets the Dialogue API service - same pattern as ScriptContext.Dialogue in NPC behaviors.
    /// </summary>
    public DialogueApiService Dialogue => _apis.Dialogue;

    /// <summary>
    ///     Gets the Effects API service - same pattern as ScriptContext.Effects in NPC behaviors.
    /// </summary>
    public EffectApiService Effects => _apis.Effects;

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Prints an object to the console output.
    /// </summary>
    /// <param name="obj">The object to print.</param>
    public void Print(object? obj)
    {
        string text = obj?.ToString() ?? "null";
        OutputAction?.Invoke(text);
        System.Console.WriteLine(text);
    }

    /// <summary>
    ///     Gets the current player entity.
    /// </summary>
    /// <returns>The player entity, or null if not found.</returns>
    public Entity? GetPlayer()
    {
        QueryDescription query = new QueryDescription().WithAll<Player>();
        var entities = new List<Entity>();
        World.Query(in query, entity => entities.Add(entity));
        return entities.FirstOrDefault();
    }

    /// <summary>
    ///     Gets all entities in the world.
    /// </summary>
    /// <returns>Collection of all entities.</returns>
    public IEnumerable<Entity> GetAllEntities()
    {
        var entities = new List<Entity>();
        World.Query(new QueryDescription(), entity => entities.Add(entity));
        return entities;
    }

    /// <summary>
    ///     Gets entities with a specific component type.
    /// </summary>
    /// <typeparam name="T">The component type to query.</typeparam>
    /// <returns>Collection of entities with the specified component.</returns>
    public IEnumerable<Entity> GetEntitiesWith<T>()
    {
        QueryDescription query = new QueryDescription().WithAll<T>();
        var entities = new List<Entity>();
        World.Query(in query, entity => entities.Add(entity));
        return entities;
    }

    /// <summary>
    ///     Counts entities with a specific component type.
    /// </summary>
    /// <typeparam name="T">The component type to count.</typeparam>
    /// <returns>Count of entities with the specified component.</returns>
    public int CountEntitiesWith<T>()
    {
        QueryDescription query = new QueryDescription().WithAll<T>();
        return World.CountEntities(in query);
    }

    /// <summary>
    ///     Counts total entities in the world.
    /// </summary>
    /// <returns>Total entity count.</returns>
    public int CountEntities()
    {
        var allQuery = new QueryDescription();
        return World.CountEntities(in allQuery);
    }

    /// <summary>
    ///     Inspects an entity and prints its component information.
    /// </summary>
    /// <param name="entity">The entity to inspect.</param>
    public void Inspect(Entity entity)
    {
        if (!World.IsAlive(entity))
        {
            Print($"Entity {entity.Id} is not alive");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Entity {entity.Id}:");

        object?[] components = entity.GetAllComponents();
        if (components.Any())
        {
            foreach (object? component in components)
            {
                if (component != null)
                {
                    sb.AppendLine($"  - {component.GetType().Name}");
                }
            }
        }
        else
        {
            sb.AppendLine("  (no components)");
        }

        Print(sb.ToString());
    }

    /// <summary>
    ///     Lists all entities with their IDs and component count.
    /// </summary>
    public void ListEntities()
    {
        var entities = GetAllEntities().ToList();
        Print($"Total entities: {entities.Count}");

        foreach (Entity entity in entities.Take(20)) // Limit to first 20 for readability
        {
            int componentCount = entity.GetAllComponents().Count();
            Print($"  Entity {entity.Id}: {componentCount} component(s)");
        }

        if (entities.Count > 20)
        {
            Print($"  ... and {entities.Count - 20} more");
        }
    }

    /// <summary>
    ///     Gets the game's current FPS or frame time info.
    /// </summary>
    public string GetPerformanceInfo()
    {
        return $"Viewport: {Graphics.Viewport.Width}x{Graphics.Viewport.Height}";
    }

    /// <summary>
    ///     Displays help information about available console methods and API.
    /// </summary>
    public void Help()
    {
        Print("");
        Print("╔════════════════════════════════════════════════════════════╗");
        Print("║          PokeSharp Console API Reference                  ║");
        Print("╚════════════════════════════════════════════════════════════╝");
        Print("");

        Print("═══ GLOBAL OBJECTS ═══");
        Print("  World       ECS World instance - query entities and components");
        Print("  Systems     SystemManager - access game systems");
        Print("  Graphics    GraphicsDevice - rendering and display info");
        Print("  Logger      ILogger - write to game logs");
        Print("  Args        Script arguments (string[] from 'load script arg1 arg2')");
        Print("");

        Print("═══ GAME API SERVICES ═══");
        Print("  These match the NPC behavior scripting API:");
        Print("");
        Print("  Player Service:");
        Print("    Player.GetMoney()               Get player's current money");
        Print("    Player.AddMoney(amount)         Add money (negative to subtract)");
        Print("    Player.GetPosition()            Get (x, y, mapId) tuple");
        Print("    Player.SetPosition(x, y)        Teleport player");
        Print("");
        Print("  Map Service:");
        Print("    Map.TransitionToMap(id, x, y)   Change to different map");
        Print("    Map.IsWalkable(x, y)            Check if tile is walkable");
        Print("");
        Print("  GameState Service:");
        Print("    GameState.SetFlag(name, value)  Set boolean flag");
        Print("    GameState.GetFlag(name)         Get boolean flag");
        Print("    GameState.SetVariable(n, v)     Set string variable");
        Print("    GameState.GetVariable(name)     Get string variable");
        Print("");
        Print("  Dialogue Service:");
        Print("    Dialogue.ShowMessage(text)      Display message box");
        Print("");
        Print("  NPC Service:");
        Print("    Npc.SetPosition(entity, x, y)   Move NPC");
        Print("    Npc.PlayAnimation(entity, anim) Change NPC animation");
        Print("");
        Print("  Effects Service:");
        Print("    Effects.ShowEffect(type, x, y)  Display visual effect");
        Print("");

        Print("═══ HELPER METHODS ═══");
        Print("  Entity Queries:");
        Print("    GetPlayer()                     Get player entity");
        Print("    GetAllEntities()                Get list of all entities");
        Print("    GetEntitiesWith<Component>()    Get entities with specific component");
        Print("    CountEntities()                 Count total entities");
        Print("    CountEntitiesWith<Component>()  Count entities with component");
        Print("");
        Print("  Debug & Inspection:");
        Print("    Print(object)                   Output to console");
        Print("    Inspect(entity)                 Show all components on entity");
        Print("    ListEntities()                  List all entities with IDs");
        Print("    GetPerformanceInfo()            Get FPS and timing info");
        Print("");

        Print("═══ COMMON EXAMPLES ═══");
        Print("  // Check player money");
        Print("  Player.GetMoney()");
        Print("");
        Print("  // Give player 1000 gold");
        Print("  Player.AddMoney(1000)");
        Print("");
        Print("  // Teleport to coordinates (10, 15) on current map");
        Print("  Player.SetPosition(10, 15)");
        Print("");
        Print("  // Change to map 3 at position (5, 5)");
        Print("  Map.TransitionToMap(3, 5, 5)");
        Print("");
        Print("  // Set and check game flags");
        Print("  GameState.SetFlag(\"met_professor\", true)");
        Print("  GameState.GetFlag(\"met_professor\")");
        Print("");
        Print("  // Count entities with Position component");
        Print("  CountEntitiesWith<Position>()");
        Print("");
        Print("  // Inspect the player entity");
        Print("  Inspect(GetPlayer())");
        Print("");

        Print("═══ ADVANCED: DIRECT ECS ACCESS ═══");
        Print("  // Query entities directly (requires Arch knowledge)");
        Print("  var query = new QueryDescription().WithAll<Position, Sprite>();");
        Print("  World.Query(in query, (Entity e, ref Position pos) => {");
        Print("      Print($\"Entity {e.Id} at ({pos.X}, {pos.Y})\");");
        Print("  });");
        Print("");

        Print("────────────────────────────────────────────────────────────");
        Print("TIP: Type 'help' (lowercase) for console commands & shortcuts");
        Print("────────────────────────────────────────────────────────────");
        Print("");
    }

    /// <summary>
    ///     Clears the console output (placeholder for console implementation).
    /// </summary>
    public void Clear()
    {
        System.Console.Clear();
    }

    #endregion
}
