using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Templates.Loading;
using PokeSharp.Engine.Input.Components;
using PokeSharp.Game.Components.Common;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Templates;

/// <summary>
///     Registers all component deserializers for the game.
/// </summary>
public static class ComponentDeserializerSetup
{
    /// <summary>
    ///     Register all game component deserializers.
    /// </summary>
    public static void RegisterAllDeserializers(
        ComponentDeserializerRegistry registry,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registry, nameof(registry));

        logger?.LogInformation("Registering component deserializers...");

        // Tile components
        RegisterTileComponents(registry);

        // Movement components
        RegisterMovementComponents(registry);

        // Rendering components
        RegisterRenderingComponents(registry);

        // Common components
        RegisterCommonComponents(registry);

        // Player components
        RegisterPlayerComponents(registry);

        // NPC components
        RegisterNpcComponents(registry);

        logger?.LogInformation(
            "Registered {Count} component deserializers",
            registry.GetRegisteredTypes().Count()
        );
    }

    private static void RegisterTileComponents(ComponentDeserializerRegistry registry)
    {
        // TilePosition
        registry.Register<TilePosition>(json =>
        {
            var x = json.GetProperty("x").GetInt32();
            var y = json.GetProperty("y").GetInt32();
            return new TilePosition(x, y);
        });

        // TileSprite
        registry.Register<TileSprite>(json =>
        {
            var tilesetId = json.GetProperty("tilesetId").GetString() ?? "default";
            var tileId = json.GetProperty("tileId").GetInt32();

            // SourceRect is optional, can be calculated by renderer
            var sourceRect = Rectangle.Empty;
            if (json.TryGetProperty("sourceRect", out var rectElement))
            {
                var x = rectElement.GetProperty("x").GetInt32();
                var y = rectElement.GetProperty("y").GetInt32();
                var width = rectElement.GetProperty("width").GetInt32();
                var height = rectElement.GetProperty("height").GetInt32();
                sourceRect = new Rectangle(x, y, width, height);
            }

            return new TileSprite(tilesetId, tileId, sourceRect);
        });

        // Elevation
        registry.Register<Elevation>(json =>
        {
            var level = json.GetProperty("level").GetByte();
            return new Elevation(level);
        });

        // TileLedge
        registry.Register<TileLedge>(json =>
        {
            var directionStr = json.GetProperty("direction").GetString();
            var direction = directionStr switch
            {
                "north" or "North" or "up" or "Up" => Direction.North,
                "south" or "South" or "down" or "Down" => Direction.South,
                "west" or "West" or "left" or "Left" => Direction.West,
                "east" or "East" or "right" or "Right" => Direction.East,
                _ => throw new ArgumentException($"Invalid direction: {directionStr}")
            };
            return new TileLedge(direction);
        });

        // EncounterZone
        registry.Register<EncounterZone>(json =>
        {
            var zoneId = json.GetProperty("zoneId").GetString() ?? "default";
            var encounterRate = json.GetProperty("encounterRate").GetInt32();
            return new EncounterZone(zoneId, encounterRate);
        });
    }

    private static void RegisterMovementComponents(ComponentDeserializerRegistry registry)
    {
        // Collision
        registry.Register<Collision>(json =>
        {
            var isSolid = json.GetProperty("isSolid").GetBoolean();
            return new Collision(isSolid);
        });

        // Position
        registry.Register<Position>(json =>
        {
            var x = json.GetProperty("x").GetInt32();
            var y = json.GetProperty("y").GetInt32();
            var mapId = 0;
            if (json.TryGetProperty("mapId", out var mapIdElement))
            {
                mapId = mapIdElement.GetInt32();
            }
            return new Position(x, y, mapId);
        });

        // Velocity
        registry.Register<Velocity>(json =>
        {
            var velocityX = json.GetProperty("velocityX").GetSingle();
            var velocityY = json.GetProperty("velocityY").GetSingle();
            return new Velocity(velocityX, velocityY);
        });

        // GridMovement
        registry.Register<GridMovement>(json =>
        {
            var tilesPerSecond = json.GetProperty("tilesPerSecond").GetSingle();
            return new GridMovement(tilesPerSecond);
        });
    }

    private static void RegisterRenderingComponents(ComponentDeserializerRegistry registry)
    {
        // Sprite - Uses Pokemon Emerald extracted sprites
        registry.Register<Sprite>(json =>
        {
            var spriteName = json.GetProperty("spriteId").GetString() ?? "boy_1";
            var category = json.GetProperty("category").GetString() ?? "generic";

            var sprite = new Sprite(spriteName, category);

            // Optional: FlipHorizontal for facing left
            if (json.TryGetProperty("flipHorizontal", out var flipElement))
            {
                sprite.FlipHorizontal = flipElement.GetBoolean();
            }

            // Optional: CurrentFrame (defaults to 0)
            if (json.TryGetProperty("currentFrame", out var frameElement))
            {
                sprite.CurrentFrame = frameElement.GetInt32();
            }

            return sprite;
        });

        // Animation
        registry.Register<Animation>(json =>
        {
            var animationName = json.GetProperty("animationName").GetString() ?? "default";
            return new Animation(animationName);
        });

        // ImageLayer
        registry.Register<ImageLayer>(json =>
        {
            var textureId = json.GetProperty("textureId").GetString() ?? "default";
            var x = json.GetProperty("x").GetSingle();
            var y = json.GetProperty("y").GetSingle();
            var opacity = 1.0f;
            if (json.TryGetProperty("opacity", out var opacityElement))
            {
                opacity = opacityElement.GetSingle();
            }
            var layerDepth = json.GetProperty("layerDepth").GetSingle();
            var layerIndex = json.GetProperty("layerIndex").GetInt32();
            return new ImageLayer(textureId, x, y, opacity, layerDepth, layerIndex);
        });
    }

    private static void RegisterCommonComponents(ComponentDeserializerRegistry registry)
    {
        // Name
        registry.Register<Name>(json =>
        {
            var name = json.GetProperty("name").GetString() ?? "Unnamed";
            return new Name(name);
        });

        // Wallet
        registry.Register<Wallet>(json =>
        {
            var money = json.GetProperty("money").GetInt32();
            return new Wallet(money);
        });
    }

    private static void RegisterPlayerComponents(ComponentDeserializerRegistry registry)
    {
        // Player (empty marker component)
        registry.Register<Player>(json =>
        {
            return new Player();
        });

        // Direction (enum component)
        registry.Register<Direction>(json =>
        {
            var directionStr = json.GetProperty("direction").GetString();
            return directionStr switch
            {
                "none" or "None" => Direction.None,
                "north" or "North" or "up" or "Up" => Direction.North,
                "south" or "South" or "down" or "Down" => Direction.South,
                "west" or "West" or "left" or "Left" => Direction.West,
                "east" or "East" or "right" or "Right" => Direction.East,
                _ => throw new ArgumentException($"Invalid direction: {directionStr}")
            };
        });

        // InputState
        registry.Register<InputState>(json =>
        {
            var pressedDirection = Direction.None;
            if (json.TryGetProperty("pressedDirection", out var dirElement))
            {
                var dirStr = dirElement.GetString();
                pressedDirection = dirStr switch
                {
                    "north" or "North" or "up" or "Up" => Direction.North,
                    "south" or "South" or "down" or "Down" => Direction.South,
                    "west" or "West" or "left" or "Left" => Direction.West,
                    "east" or "East" or "right" or "Right" => Direction.East,
                    _ => Direction.None
                };
            }

            var actionPressed = false;
            if (json.TryGetProperty("actionPressed", out var actionElement))
            {
                actionPressed = actionElement.GetBoolean();
            }

            return new InputState
            {
                PressedDirection = pressedDirection,
                ActionPressed = actionPressed
            };
        });
    }

    private static void RegisterNpcComponents(ComponentDeserializerRegistry registry)
    {
        // Npc
        registry.Register<Npc>(json =>
        {
            var npcId = json.GetProperty("npcId").GetString() ?? "";
            var isTrainer = false;
            if (json.TryGetProperty("isTrainer", out var isTrainerElement))
            {
                isTrainer = isTrainerElement.GetBoolean();
            }

            return new Npc
            {
                NpcId = npcId,
                IsTrainer = isTrainer
            };
        });

        // Behavior
        registry.Register<Behavior>(json =>
        {
            var behaviorTypeId = json.GetProperty("behaviorTypeId").GetString() ?? "";
            var isActive = true;
            if (json.TryGetProperty("isActive", out var isActiveElement))
            {
                isActive = isActiveElement.GetBoolean();
            }

            return new Behavior
            {
                BehaviorTypeId = behaviorTypeId,
                IsActive = isActive
            };
        });
    }
}


