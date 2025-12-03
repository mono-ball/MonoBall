using PokeSharp.Game.Scripting.Runtime;
using EnhancedLedges.Events;

/// <summary>
///     Visual Effects Handler - manages animations, particles, and sounds for ledge interactions.
///     Demonstrates visual feedback systems and effect coordination.
/// </summary>
/// <remarks>
///     Effects:
///     - Jump arc animation (parabolic curve)
///     - Dust particles on landing
///     - Ledge crack visuals (progressive damage)
///     - Crumble animation (falling debris)
///     - Sound effects (jump, land, crumble)
///     - Boost glow effect
/// </remarks>
public class VisualEffectsBehavior : ScriptBase
{
    private const float JUMP_ARC_HEIGHT = 16.0f; // pixels
    private const float JUMP_DURATION = 0.3f; // seconds
    private const int DUST_PARTICLE_COUNT = 5;
    private const int CRUMBLE_PARTICLE_COUNT = 12;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Context.Logger.LogInformation("Visual effects handler initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Jump arc animation and landing effects
        On<LedgeJumpedEvent>((evt) =>
        {
            Context.Logger.LogDebug($"Playing jump effects at ({evt.TileX}, {evt.TileY})");

            // Play jump arc animation
            PlayJumpArcAnimation(evt.Entity, evt.Direction, evt.JumpHeight);

            // Schedule landing effects (after animation completes)
            ScheduleLandingEffects(evt.Entity, JUMP_DURATION);

            // Play jump sound
            PlaySound("ledge_jump", volume: 0.7f);

            // Show boost glow if boosted
            if (evt.IsBoosted)
            {
                ShowBoostGlowEffect(evt.Entity);
                PlaySound("jump_boost", volume: 0.6f);
            }
        });

        // Crumble visual effects
        On<LedgeCrumbledEvent>((evt) =>
        {
            Context.Logger.LogDebug($"Playing crumble effects at ({evt.TileX}, {evt.TileY})");

            // Play crumble animation
            PlayCrumbleAnimation(evt.TileX, evt.TileY);

            // Spawn falling debris particles
            SpawnCrumbleParticles(evt.TileX, evt.TileY);

            // Play crumble sound
            PlaySound("ledge_crumble", volume: 0.8f);

            // Camera shake if player was on it
            if (evt.WasPlayerOn)
            {
                TriggerCameraShake(intensity: 0.3f, duration: 0.4f);
            }
        });

        // Boost activation effects
        On<JumpBoostActivatedEvent>((evt) =>
        {
            Context.Logger.LogDebug($"Playing boost activation effects for entity {evt.Entity.Id}");

            // Show boost aura effect
            ShowBoostAura(evt.Entity, evt.DurationSeconds);

            // Play buff sound
            PlaySound("buff_activated", volume: 0.65f);

            // Show boost icon in UI
            ShowBoostStatusIcon(evt.Entity, evt.ExpiresAt);
        });
    }

    private void PlayJumpArcAnimation(Entity entity, int direction, float jumpHeight)
    {
        Context.Logger.LogDebug($"Jump arc: entity={entity.Id}, dir={direction}, height={jumpHeight}x");

        // Would calculate parabolic arc based on:
        // - Start position (current entity position)
        // - End position (target tile)
        // - Arc height (JUMP_ARC_HEIGHT * jumpHeight)
        // - Duration (JUMP_DURATION)

        // Animation system would interpolate position along arc curve
        var arcHeight = JUMP_ARC_HEIGHT * jumpHeight;
        Context.Logger.LogDebug($"Animating jump arc: height={arcHeight}px, duration={JUMP_DURATION}s");
    }

    private void ScheduleLandingEffects(Entity entity, float delay)
    {
        // Would use timer/scheduler system to trigger landing effects after delay
        Context.Logger.LogDebug($"Scheduled landing effects in {delay}s");

        // Landing effects would include:
        // - Dust particle burst
        // - Ground impact animation
        // - Landing sound effect
    }

    private void SpawnDustParticles(int x, int y)
    {
        Context.Logger.LogDebug($"Spawning {DUST_PARTICLE_COUNT} dust particles at ({x}, {y})");

        // Would spawn particle effects:
        // - Random velocities (slight upward + outward)
        // - Fade out over 0.5 seconds
        // - Brownish-gray color
    }

    private void PlayCrumbleAnimation(int x, int y)
    {
        Context.Logger.LogDebug($"Playing crumble animation at ({x}, {y})");

        // Multi-stage crumble animation:
        // 1. Crack lines appear (0.1s)
        // 2. Tile fragments (0.2s)
        // 3. Pieces fall away (0.3s)
        // 4. Dust cloud (0.2s)
    }

    private void SpawnCrumbleParticles(int x, int y)
    {
        Context.Logger.LogDebug($"Spawning {CRUMBLE_PARTICLE_COUNT} crumble particles at ({x}, {y})");

        // Would spawn falling debris particles:
        // - Random stone fragments
        // - Gravity affected
        // - Fade out as they fall
        // - Various sizes
    }

    private void ShowBoostGlowEffect(Entity entity)
    {
        Context.Logger.LogDebug($"Showing boost glow for entity {entity.Id}");

        // Would add temporary glow effect:
        // - Bright yellow/white outline
        // - Pulsing animation
        // - Lasts for duration of jump
    }

    private void ShowBoostAura(Entity entity, float duration)
    {
        Context.Logger.LogDebug($"Showing boost aura for {duration}s");

        // Would add persistent aura effect:
        // - Circular energy ring around entity
        // - Rotating animation
        // - Color transitions
        // - Fades out near expiration
    }

    private void ShowBoostStatusIcon(Entity entity, DateTime expiresAt)
    {
        Context.Logger.LogDebug($"Showing boost status icon, expires at {expiresAt}");

        // Would display UI icon:
        // - Boot/shoe icon in status bar
        // - Countdown timer
        // - Flashes when near expiration
    }

    private void TriggerCameraShake(float intensity, float duration)
    {
        Context.Logger.LogDebug($"Camera shake: intensity={intensity}, duration={duration}s");

        // Would trigger camera shake effect:
        // - Random offset applied to camera position
        // - Decreases over duration
        // - Intensity affects max offset
    }

    private void PlaySound(string soundId, float volume = 1.0f)
    {
        Context.Logger.LogDebug($"Playing sound: {soundId} (volume={volume})");

        // Would play sound effect:
        // - Load from audio assets
        // - Apply volume
        // - 3D spatial audio if applicable
    }
}

return new VisualEffectsBehavior();
