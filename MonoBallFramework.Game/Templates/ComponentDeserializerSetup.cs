using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Core.Templates.Loading;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Input.Components;

namespace MonoBallFramework.Game.Templates;

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
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(registry);

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
        registry.Register(json =>
        {
            int x = json.GetProperty("x").GetInt32();
            int y = json.GetProperty("y").GetInt32();
            return new TilePosition(x, y);
        });

        // TileSprite
        registry.Register(json =>
        {
            string tilesetId = json.GetProperty("tilesetId").GetString() ?? "default";
            int tileId = json.GetProperty("tileId").GetInt32();

            // SourceRect is optional, can be calculated by renderer
            Rectangle sourceRect = Rectangle.Empty;
            if (json.TryGetProperty("sourceRect", out JsonElement rectElement))
            {
                int x = rectElement.GetProperty("x").GetInt32();
                int y = rectElement.GetProperty("y").GetInt32();
                int width = rectElement.GetProperty("width").GetInt32();
                int height = rectElement.GetProperty("height").GetInt32();
                sourceRect = new Rectangle(x, y, width, height);
            }

            return new TileSprite(tilesetId, tileId, sourceRect);
        });

        // Elevation
        registry.Register(json =>
        {
            byte level = json.GetProperty("level").GetByte();
            return new Elevation(level);
        });

        // EncounterZone
        registry.Register(json =>
        {
            string zoneId = json.GetProperty("zoneId").GetString() ?? "default";
            int encounterRate = json.GetProperty("encounterRate").GetInt32();
            return new EncounterZone(zoneId, encounterRate);
        });

        // TileBehavior
        registry.Register(json =>
        {
            string behaviorTypeId = json.GetProperty("behaviorTypeId").GetString() ?? "";
            bool isActive = true;
            if (json.TryGetProperty("isActive", out JsonElement isActiveElement))
            {
                isActive = isActiveElement.GetBoolean();
            }

            return new TileBehavior(behaviorTypeId) { IsActive = isActive };
        });
    }

    private static void RegisterMovementComponents(ComponentDeserializerRegistry registry)
    {
        // Collision
        registry.Register(json =>
        {
            bool isSolid = json.GetProperty("isSolid").GetBoolean();
            return new Collision(isSolid);
        });

        // Position
        registry.Register(json =>
        {
            int x = json.GetProperty("x").GetInt32();
            int y = json.GetProperty("y").GetInt32();
            GameMapId? mapId = null;
            if (json.TryGetProperty("mapId", out JsonElement mapIdElement))
            {
                string mapIdStr = mapIdElement.GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(mapIdStr))
                {
                    mapId = new GameMapId(mapIdStr);
                }
            }

            return new Position(x, y, mapId);
        });

        // GridMovement
        registry.Register(json =>
        {
            float tilesPerSecond = json.GetProperty("tilesPerSecond").GetSingle();
            return new GridMovement(tilesPerSecond);
        });
    }

    private static void RegisterRenderingComponents(ComponentDeserializerRegistry registry)
    {
        // Sprite - Uses Pokemon Emerald extracted sprites
        // Format: spriteId should be full ID like "base:sprite:npcs/generic_twin"
        // or path format like "npcs/generic_twin"
        registry.Register(json =>
        {
            string spriteIdStr = json.GetProperty("spriteId").GetString() ?? "npcs/generic_boy_1";

            // Support both full ID format and path-only format
            GameSpriteId spriteId;
            if (spriteIdStr.Contains(':'))
            {
                // Full ID format: "base:sprite:npcs/generic_twin"
                spriteId = new GameSpriteId(spriteIdStr);
            }
            else
            {
                // Path format: "npcs/generic_twin" -> "base:sprite:npcs/generic_twin"
                spriteId = new GameSpriteId($"base:sprite:{spriteIdStr}");
            }

            var sprite = new Sprite(spriteId);

            // Optional: FlipHorizontal for facing left
            if (json.TryGetProperty("flipHorizontal", out JsonElement flipElement))
            {
                sprite.FlipHorizontal = flipElement.GetBoolean();
            }

            // Optional: CurrentFrame (defaults to 0)
            if (json.TryGetProperty("currentFrame", out JsonElement frameElement))
            {
                sprite.CurrentFrame = frameElement.GetInt32();
            }

            return sprite;
        });

        // Animation
        registry.Register(json =>
        {
            string animationName = json.GetProperty("animationName").GetString() ?? "default";
            return new Animation(animationName);
        });

        // ImageLayer
        registry.Register(json =>
        {
            string textureId = json.GetProperty("textureId").GetString() ?? "default";
            float x = json.GetProperty("x").GetSingle();
            float y = json.GetProperty("y").GetSingle();
            float opacity = 1.0f;
            if (json.TryGetProperty("opacity", out JsonElement opacityElement))
            {
                opacity = opacityElement.GetSingle();
            }

            float layerDepth = json.GetProperty("layerDepth").GetSingle();
            int layerIndex = json.GetProperty("layerIndex").GetInt32();
            return new ImageLayer(textureId, x, y, opacity, layerDepth, layerIndex);
        });
    }

    private static void RegisterCommonComponents(ComponentDeserializerRegistry registry)
    {
        // Name
        registry.Register(json =>
        {
            string name = json.GetProperty("name").GetString() ?? "Unnamed";
            return new Name(name);
        });

        // Wallet
        registry.Register(json =>
        {
            int money = json.GetProperty("money").GetInt32();
            return new Wallet(money);
        });
    }

    private static void RegisterPlayerComponents(ComponentDeserializerRegistry registry)
    {
        // Player (empty marker component)
        registry.Register(json =>
        {
            return new Player();
        });

        // Direction (enum component)
        registry.Register(json =>
        {
            string? directionStr = json.GetProperty("direction").GetString();
            return directionStr switch
            {
                "none" or "None" => Direction.None,
                "north" or "North" or "up" or "Up" => Direction.North,
                "south" or "South" or "down" or "Down" => Direction.South,
                "west" or "West" or "left" or "Left" => Direction.West,
                "east" or "East" or "right" or "Right" => Direction.East,
                _ => throw new ArgumentException($"Invalid direction: {directionStr}"),
            };
        });

        // InputState
        registry.Register(json =>
        {
            Direction pressedDirection = Direction.None;
            if (json.TryGetProperty("pressedDirection", out JsonElement dirElement))
            {
                string? dirStr = dirElement.GetString();
                pressedDirection = dirStr switch
                {
                    "north" or "North" or "up" or "Up" => Direction.North,
                    "south" or "South" or "down" or "Down" => Direction.South,
                    "west" or "West" or "left" or "Left" => Direction.West,
                    "east" or "East" or "right" or "Right" => Direction.East,
                    _ => Direction.None,
                };
            }

            bool actionPressed = false;
            if (json.TryGetProperty("actionPressed", out JsonElement actionElement))
            {
                actionPressed = actionElement.GetBoolean();
            }

            return new InputState
            {
                PressedDirection = pressedDirection,
                ActionPressed = actionPressed,
            };
        });
    }

    private static void RegisterNpcComponents(ComponentDeserializerRegistry registry)
    {
        // Npc
        registry.Register(json =>
        {
            string npcId = json.GetProperty("npcId").GetString() ?? "";
            bool isTrainer = false;
            if (json.TryGetProperty("isTrainer", out JsonElement isTrainerElement))
            {
                isTrainer = isTrainerElement.GetBoolean();
            }

            return new Npc { NpcId = GameNpcId.Create(npcId), IsTrainer = isTrainer };
        });

        // Behavior
        registry.Register(json =>
        {
            string behaviorTypeId = json.GetProperty("behaviorTypeId").GetString() ?? "";
            bool isActive = true;
            if (json.TryGetProperty("isActive", out JsonElement isActiveElement))
            {
                isActive = isActiveElement.GetBoolean();
            }

            return new Behavior { BehaviorTypeId = behaviorTypeId, IsActive = isActive };
        });
    }
}
