using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Modding;
using PokeSharp.Engine.Core.Templates;
using PokeSharp.Engine.Core.Templates.Loading;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Initializes the TemplateCache asynchronously by loading base templates, mods, and applying patches.
///     This allows template loading to happen during async initialization instead of blocking DI registration.
/// </summary>
public class TemplateCacheInitializer
{
    private readonly TemplateCache _templateCache;
    private readonly JsonTemplateLoader _jsonLoader;
    private readonly ModLoader _modLoader;
    private readonly PatchFileLoader _patchFileLoader;
    private readonly PatchApplicator _patchApplicator;
    private readonly ILogger<TemplateCacheInitializer>? _logger;

    public TemplateCacheInitializer(
        TemplateCache templateCache,
        JsonTemplateLoader jsonLoader,
        ModLoader modLoader,
        PatchFileLoader patchFileLoader,
        PatchApplicator patchApplicator,
        ILogger<TemplateCacheInitializer>? logger = null
    )
    {
        _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
        _jsonLoader = jsonLoader ?? throw new ArgumentNullException(nameof(jsonLoader));
        _modLoader = modLoader ?? throw new ArgumentNullException(nameof(modLoader));
        _patchFileLoader = patchFileLoader ?? throw new ArgumentNullException(nameof(patchFileLoader));
        _patchApplicator = patchApplicator ?? throw new ArgumentNullException(nameof(patchApplicator));
        _logger = logger;
    }

    /// <summary>
    ///     Initializes the template cache by loading base templates, mods, and applying patches.
    /// </summary>
    public async Task InitializeAsync(string templatesBasePath = "Assets/Templates")
    {
        // Load base game JSON templates as JSON (before deserialization)
        var templateJsonCache = await _jsonLoader.LoadTemplateJsonAsync(templatesBasePath, recursive: true);

        _logger?.LogInformation(
            "[steelblue1]WF[/] Template JSON loaded | count: [yellow]{Count}[/], source: [cyan]base[/]",
            templateJsonCache.Count
        );

        // Load and apply mods
        var mods = _modLoader.DiscoverMods();
        var sortedMods = _modLoader.SortByLoadOrder(mods);

        _logger?.LogInformation(
            "[steelblue1]WF[/] Mod system initializing | discovered: [yellow]{Count}[/]",
            sortedMods.Count
        );

        foreach (var mod in sortedMods)
        {
            _logger?.LogInformation(
                "[steelblue1]WF[/] Loading mod | id: [cyan]{ModId}[/], version: [cyan]{Version}[/]",
                mod.Manifest.ModId,
                mod.Manifest.Version
            );

            // Load mod templates as JSON (new content)
            if (mod.Manifest.ContentFolders.TryGetValue("Templates", out var templatesPath))
            {
                var modTemplatesDir = mod.ResolvePath(templatesPath);
                if (Directory.Exists(modTemplatesDir))
                {
                    var modJsonCache = await _jsonLoader.LoadTemplateJsonAsync(modTemplatesDir, recursive: true);

                    // Add mod templates to the main cache
                    foreach (var (path, json) in modJsonCache.GetAll())
                    {
                        templateJsonCache.Add(path, json);

                        // Extract templateId for logging
                        if (
                            json is System.Text.Json.Nodes.JsonObject obj
                            && obj.TryGetPropertyValue("templateId", out var idNode)
                        )
                        {
                            var templateId = idNode?.ToString().Trim('"');
                            _logger?.LogInformation("    [green]+[/] [cyan]{TemplateId}[/]", templateId);
                        }
                    }
                }
            }

            // Apply patches from mod (patch the JSON before deserialization)
            var patches = _patchFileLoader.LoadModPatches(mod);
            foreach (var patch in patches)
            {
                try
                {
                    // Get the target template JSON
                    var targetJson = templateJsonCache.GetByTemplateId(patch.Target);
                    if (targetJson == null)
                    {
                        _logger?.LogWarning(
                            "    [orange3]![/] Patch target not found | target: [cyan]{Target}[/]",
                            patch.Target
                        );
                        continue;
                    }

                    // Apply patch to JSON
                    var patchedJson = _patchApplicator.ApplyPatch(targetJson, patch);
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
                    _logger?.LogError(ex, "    [red]![/] Patch error | target: [cyan]{Target}[/]", patch.Target);
                }
            }
        }

        // Now deserialize all templates (base game + mods + patches applied)
        foreach (var (path, json) in templateJsonCache.GetAll())
        {
            try
            {
                var template = _jsonLoader.DeserializeTemplate(json, path);
                _templateCache.Register(template);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[red]✗[/] Template deserialization failed | path: [cyan]{Path}[/]", path);
            }
        }

        _logger?.LogInformation("[skyblue1]▶[/] Template cache ready | count: [yellow]{Count}[/]", _templateCache.Count);
    }
}

