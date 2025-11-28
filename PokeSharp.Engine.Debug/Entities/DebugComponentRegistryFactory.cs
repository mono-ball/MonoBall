using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components;

namespace PokeSharp.Engine.Debug.Entities;

/// <summary>
/// Factory for creating a DebugComponentRegistry with all known game components.
/// </summary>
public static class DebugComponentRegistryFactory
{
    /// <summary>
    /// Creates a DebugComponentRegistry with all standard game components registered.
    /// </summary>
    public static DebugComponentRegistry CreateDefault()
    {
        var registry = new DebugComponentRegistry();

        // Movement & Position components (high priority for entity naming)
        registry.Register<Position>(
            "Position",
            pos => new Dictionary<string, string>
            {
                ["X"] = pos.X.ToString("F1"),
                ["Y"] = pos.Y.ToString("F1"),
                ["Position"] = $"({pos.X:F1}, {pos.Y:F1})"
            },
            category: "Movement",
            priority: 10
        );

        registry.Register<TilePosition>(
            "TilePosition",
            tp => new Dictionary<string, string>
            {
                ["TileX"] = tp.X.ToString(),
                ["TileY"] = tp.Y.ToString(),
                ["TilePosition"] = $"({tp.X}, {tp.Y})"
            },
            category: "Movement",
            priority: 9
        );

        registry.Register<Elevation>(
            "Elevation",
            elev => new Dictionary<string, string>
            {
                ["Elevation"] = elev.Value.ToString()
            },
            category: "Movement",
            priority: 5
        );

        registry.Register<GridMovement>(
            "GridMovement",
            gm => new Dictionary<string, string>
            {
                ["Direction"] = gm.FacingDirection.ToString(),
                ["IsMoving"] = gm.IsMoving.ToString()
            },
            category: "Movement",
            priority: 8
        );

        registry.Register<MovementRequest>("MovementRequest", category: "Movement", priority: 3);

        registry.Register<Collision>(
            "Collision",
            col => new Dictionary<string, string>
            {
                ["IsSolid"] = col.IsSolid.ToString()
            },
            category: "Movement",
            priority: 4
        );

        // Player components (highest priority for naming)
        registry.Register<Player>("Player", category: "Entity", priority: 100);

        // NPC components
        registry.Register<Npc>("Npc", category: "Entity", priority: 90);
        registry.Register<Behavior>("Behavior", category: "NPC", priority: 5);
        registry.Register<Interaction>("Interaction", category: "NPC", priority: 5);
        registry.Register<MovementRoute>("MovementRoute", category: "NPC", priority: 5);

        // Rendering components
        registry.Register<Sprite>("Sprite", category: "Rendering", priority: 20);
        registry.Register<Animation>("Animation", category: "Rendering", priority: 15);

        // Tile components
        registry.Register<TileSprite>("TileSprite", category: "Tile", priority: 50);
        registry.Register<AnimatedTile>("AnimatedTile", category: "Tile", priority: 45);
        registry.Register<TileBehavior>("TileBehavior", category: "Tile", priority: 5);

        // Pooling
        registry.Register<Pooled>("Pooled", category: "System", priority: 1);

        return registry;
    }
}

