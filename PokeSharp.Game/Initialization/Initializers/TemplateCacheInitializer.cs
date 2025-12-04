using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Engine.Core.Modding;
using PokeSharp.Game.Engine.Core.Templates;
using PokeSharp.Game.Engine.Core.Templates.Loading;
using PokeSharp.Game.Infrastructure.Services;

namespace PokeSharp.Game.Initialization.Initializers;

/// <summary>
///     Initializes the TemplateCache asynchronously by loading base templates, mods, and applying patches.
///     This allows template loading to happen during async initialization instead of blocking DI registration.
/// </summary>
public class TemplateCacheInitializer
{
    private readonly JsonTemplateLoader _jsonLoader;
    private readonly ILogger<TemplateCacheInitializer>? _logger;
    private readonly ModLoader _modLoader;
    private readonly PatchApplicator _patchApplicator;
    private readonly PatchFileLoader _patchFileLoader;
    private readonly IAssetPathResolver _pathResolver;
    private readonly TemplateCache _templateCache;

    public TemplateCacheInitializer(
        TemplateCache templateCache,
        JsonTemplateLoader jsonLoader,
        ModLoader modLoader,
        PatchFileLoader patchFileLoader,
        PatchApplicator patchApplicator,
        IAssetPathResolver pathResolver,
        ILogger<TemplateCacheInitializer>? logger = null
    )
    {
        _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
        _jsonLoader = jsonLoader ?? throw new ArgumentNullException(nameof(jsonLoader));
        _modLoader = modLoader ?? throw new ArgumentNullException(nameof(modLoader));
        _patchFileLoader =
            patchFileLoader ?? throw new ArgumentNullException(nameof(patchFileLoader));
        _patchApplicator =
            patchApplicator ?? throw new ArgumentNullException(nameof(patchApplicator));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger;
    }

    /// <summary>
    ///     Initializes the template cache by loading base templates, mods, and applying patches.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Resolve the templates path using the centralized path resolver
        string templatesBasePath = _pathResolver.Resolve("Templates");

        _logger?.LogDebug("Loading templates from: {Path}", templatesBasePath);

        // Load base game JSON templates as JSON (before deserialization)
        TemplateJsonCache templateJsonCache = await _jsonLoader.LoadTemplateJsonAsync(
            templatesBasePath
        );

        _logger?.LogInformation(
            "[steelblue1]WF[/] Template JSON loaded | count: [yellow]{Count}[/], source: [cyan]base[/]",
            templateJsonCache.Count
        );

        // Load and apply mods
        List<LoadedMod> mods = _modLoader.DiscoverMods();
        List<LoadedMod> sortedMods = _modLoader.SortByLoadOrder(mods);

        _logger?.LogInformation(
            "[steelblue1]WF[/] Mod system initializing | discovered: [yellow]{Count}[/]",
            sortedMods.Count
        );

        foreach (LoadedMod mod in sortedMods)
        {
            _logger?.LogInformation(
                "[steelblue1]WF[/] Loading mod | id: [cyan]{ModId}[/], version: [cyan]{Version}[/]",
                mod.Manifest.ModId,
                mod.Manifest.Version
            );

            // Load mod templates as JSON (new content)
            if (mod.Manifest.ContentFolders.TryGetValue("Templates", out string? templatesPath))
            {
                string modTemplatesDir = mod.ResolvePath(templatesPath);
                if (Directory.Exists(modTemplatesDir))
                {
                    TemplateJsonCache modJsonCache = await _jsonLoader.LoadTemplateJsonAsync(
                        modTemplatesDir
                    );

                    // Add mod templates to the main cache
                    foreach ((string path, JsonNode json) in modJsonCache.GetAll())
                    {
                        templateJsonCache.Add(path, json);

                        // Extract templateId for logging
                        if (
                            json is JsonObject obj
                            && obj.TryGetPropertyValue("templateId", out JsonNode? idNode)
                        )
                        {
                            string? templateId = idNode?.ToString().Trim('"');
                            _logger?.LogInformation(
                                "    [green]+[/] [cyan]{TemplateId}[/]",
                                templateId
                            );
                        }
                    }
                }
            }

            // Apply patches from mod (patch the JSON before deserialization)
            List<ModPatch> patches = _patchFileLoader.LoadModPatches(mod);
            foreach (ModPatch patch in patches)
            {
                try
                {
                    // Get the target template JSON
                    JsonNode? targetJson = templateJsonCache.GetByTemplateId(patch.Target);
                    if (targetJson == null)
                    {
                        _logger?.LogWarning(
                            "    [orange3]![/] Patch target not found | target: [cyan]{Target}[/]",
                            patch.Target
                        );
                        continue;
                    }

                    // Apply patch to JSON
                    JsonNode? patchedJson = _patchApplicator.ApplyPatch(targetJson, patch);
                    if (patchedJson == null)
                    {
                        _logger?.LogWarning(
                            "    [orange3]![/] Patch failed | target: [cyan]{Target}[/]",
                            patch.Target
                        );
                        continue;
                    }

                    // Update the JSON cache with patched version
                    templateJsonCache.Update(patch.Target, patchedJson);
                    _logger?.LogInformation(
                        "    [green]*[/] [cyan]{Target}[/] | {Desc}",
                        patch.Target,
                        patch.Description
                    );
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        ex,
                        "    [red]![/] Patch error | target: [cyan]{Target}[/]",
                        patch.Target
                    );
                }
            }
        }

        // Now deserialize all templates (base game + mods + patches applied)
        foreach ((string path, JsonNode json) in templateJsonCache.GetAll())
        {
            try
            {
                EntityTemplate template = _jsonLoader.DeserializeTemplate(json, path);
                _templateCache.Register(template);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "[red]✗[/] Template deserialization failed | path: [cyan]{Path}[/]",
                    path
                );
            }
        }

        _logger?.LogInformation(
            "[skyblue1]▶[/] Template cache ready | count: [yellow]{Count}[/]",
            _templateCache.Count
        );
    }
}
