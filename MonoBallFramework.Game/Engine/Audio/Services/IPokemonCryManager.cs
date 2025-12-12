namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Manager for Pokemon cries (vocalizations).
///     Handles the 800+ unique Pokemon cries with efficient loading and playback.
///     Supports form variations, gender differences, and pitch adjustments.
/// </summary>
public interface IPokemonCryManager : IDisposable
{
    /// <summary>
    ///     Plays a Pokemon's cry by National Dex species ID.
    /// </summary>
    /// <param name="speciesId">The Pokemon species ID (National Dex number, 1-1025).</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default cry volume.</param>
    /// <param name="pitch">Pitch adjustment for forms/gender differences (-1.0 to 1.0, default: null for no adjustment).</param>
    /// <returns>True if the cry was played successfully; false if species not found or playback failed.</returns>
    bool PlayCry(int speciesId, float? volume = null, float? pitch = null);

    /// <summary>
    ///     Plays a Pokemon's cry by species name (case-insensitive).
    /// </summary>
    /// <param name="speciesName">The Pokemon species name (e.g., "Pikachu", "Charizard").</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default cry volume.</param>
    /// <param name="pitch">Pitch adjustment for forms/gender differences (-1.0 to 1.0, default: null for no adjustment).</param>
    /// <returns>True if the cry was played successfully; false if species not found or playback failed.</returns>
    bool PlayCry(string speciesName, float? volume = null, float? pitch = null);

    /// <summary>
    ///     Plays a Pokemon's cry with form-specific variations.
    ///     Supports regional forms, mega evolutions, and other form differences.
    /// </summary>
    /// <param name="speciesId">The Pokemon species ID (National Dex number).</param>
    /// <param name="formId">The form ID (0 for base form, varies by species for alternate forms).</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default cry volume.</param>
    /// <param name="pitch">Pitch adjustment (-1.0 to 1.0, default: null for no adjustment).</param>
    /// <returns>True if the cry was played successfully; false if species/form not found or playback failed.</returns>
    bool PlayCryWithForm(int speciesId, int formId, float? volume = null, float? pitch = null);

    /// <summary>
    ///     Preloads Pokemon cries into memory for faster playback.
    ///     Useful for loading cries for a specific route, area, or upcoming battle.
    /// </summary>
    /// <param name="speciesIds">Array of National Dex species IDs to preload.</param>
    void PreloadCries(params int[] speciesIds);

    /// <summary>
    ///     Unloads Pokemon cries from memory to free resources.
    ///     Cannot unload cries that are currently playing.
    /// </summary>
    /// <param name="speciesIds">Array of National Dex species IDs to unload.</param>
    void UnloadCries(params int[] speciesIds);

    /// <summary>
    ///     Clears all cached Pokemon cries from memory.
    ///     Useful for freeing memory when changing areas or during loading screens.
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     Gets the number of Pokemon cries currently loaded in memory.
    ///     Useful for monitoring memory usage and cache effectiveness.
    /// </summary>
    int LoadedCryCount { get; }
}
