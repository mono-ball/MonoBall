using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

/// <summary>
/// Rain weather effect - GLOBAL script (no entity).
/// Demonstrates ScriptContext with null entity.
/// </summary>
public class RainWeather : TypeScriptBase
{
    private const float RAIN_INTENSITY_LIGHT = 0.3f;
    private const float RAIN_INTENSITY_MEDIUM = 0.6f;
    private const float RAIN_INTENSITY_HEAVY = 0.9f;

    private float currentIntensity = RAIN_INTENSITY_MEDIUM;
    private float intensityChangeTimer = 0f;
    private const float INTENSITY_CHANGE_INTERVAL = 10.0f;

    public override void OnActivated(ScriptContext ctx)
    {
        if (ctx.IsGlobalScript)
        {
            ctx.Logger.LogInformation(
                "Rain weather activated globally with intensity {Intensity}",
                currentIntensity
            );

            // Apply rain tint to all sprites
            ApplyRainTint(ctx, currentIntensity);

            // Could also spawn rain particle effects here
            // CreateRainParticles(ctx);
        }
        else
        {
            ctx.Logger.LogWarning(
                "Weather script should be run as global script, not on entity {Entity}",
                ctx.Entity
            );
        }
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Rain could update particle effects, sound, etc
        // ctx.Entity is NULL for global scripts

        if (ctx.IsGlobalScript)
        {
            intensityChangeTimer += deltaTime;

            // Randomly change rain intensity every 10 seconds
            if (intensityChangeTimer >= INTENSITY_CHANGE_INTERVAL)
            {
                intensityChangeTimer = 0f;

                var rand = TypeScriptBase.Random();
                currentIntensity = rand switch
                {
                    < 0.33f => RAIN_INTENSITY_LIGHT,
                    < 0.66f => RAIN_INTENSITY_MEDIUM,
                    _ => RAIN_INTENSITY_HEAVY,
                };

                ctx.Logger.LogDebug("Rain intensity changed to {Intensity}", currentIntensity);
                ApplyRainTint(ctx, currentIntensity);
            }

            // Periodic rain effects
            if (TypeScriptBase.Random() < 0.01f) // 1% chance per tick
            {
                ctx.Logger.LogTrace("Thunder sound effect triggered");
                // Play thunder sound effect
            }

            ctx.Logger.LogTrace("Rain tick (global) - Intensity: {Intensity}", currentIntensity);
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        if (ctx.IsGlobalScript)
        {
            ctx.Logger.LogInformation("Rain weather deactivated");

            // Remove rain tint
            var query = ctx.World.Query<Sprite>();
            query.ForEach(
                (ref Sprite sprite) =>
                {
                    sprite.Tint = Color.White;
                }
            );

            // Remove rain particle effects
            // DestroyRainParticles(ctx);
        }
    }

    private void ApplyRainTint(ScriptContext ctx, float intensity)
    {
        var rainColor = Color.Lerp(Color.White, Color.LightBlue, intensity);

        var query = ctx.World.Query<Sprite>();
        int spriteCount = 0;

        query.ForEach(
            (ref Sprite sprite) =>
            {
                sprite.Tint = rainColor;
                spriteCount++;
            }
        );

        ctx.Logger.LogDebug(
            "Applied rain tint to {Count} sprites with intensity {Intensity}",
            spriteCount,
            intensity
        );
    }
}

return new RainWeather();
