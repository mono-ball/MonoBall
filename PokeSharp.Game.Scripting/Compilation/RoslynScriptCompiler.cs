using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Arch.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.HotReload.Compilation;
using PokeSharp.Game.Scripting.Runtime;
using DiagnosticSeverity = PokeSharp.Game.Scripting.HotReload.Compilation.DiagnosticSeverity;

namespace PokeSharp.Game.Scripting.Compilation;

/// <summary>
///     Roslyn-based script compiler with content-based SHA256 caching for compilation results.
///     Implements async compilation support, comprehensive error handling, and assembly management.
/// </summary>
public class RoslynScriptCompiler : IScriptCompiler
{
    /// <summary>
    ///     Cache of compiled assemblies keyed by content hash (SHA256).
    ///     This ensures scripts with identical content reuse the same compiled assembly.
    /// </summary>
    private readonly ConcurrentDictionary<string, CachedCompilation> _compilationCache = new();

    /// <summary>
    ///     Compilation options for Roslyn.
    /// </summary>
    private readonly CSharpCompilationOptions _compilationOptions;

    /// <summary>
    ///     Global using directives to include in all scripts.
    /// </summary>
    private readonly List<string> _globalUsings;

    private readonly ILogger<RoslynScriptCompiler> _logger;

    /// <summary>
    ///     Metadata references for compilation (assemblies).
    /// </summary>
    private readonly List<MetadataReference> _metadataReferences;

    public RoslynScriptCompiler(ILogger<RoslynScriptCompiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize metadata references from ScriptCompilationOptions
        _metadataReferences = GetDefaultMetadataReferences();

        // Initialize global usings
        _globalUsings = GetDefaultGlobalUsings();

        // Configure compilation options
        _compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: false,
            checkOverflow: false
        );

        _logger.LogInformation(
            "RoslynScriptCompiler initialized with {RefCount} references and {UsingCount} global usings",
            _metadataReferences.Count,
            _globalUsings.Count
        );
    }

    /// <summary>
    ///     Compile a C# script file asynchronously with content-based caching.
    /// </summary>
    /// <param name="filePath">Full path to the .cs script file.</param>
    /// <returns>CompilationResult with success status, type, and diagnostics.</returns>
    public async Task<CompilationResult> CompileScriptAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { "File path cannot be null or empty" },
            };
        }

        if (!File.Exists(filePath))
        {
            _logger.LogError("Script file not found: {FilePath}", filePath);
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"File not found: {filePath}" },
            };
        }

        try
        {
            // Read script content
            string scriptContent = await File.ReadAllTextAsync(filePath);

            // Compute content hash for caching
            string contentHash = ComputeContentHash(scriptContent);

            // Check cache first
            if (_compilationCache.TryGetValue(contentHash, out CachedCompilation? cached))
            {
                _logger.LogDebug(
                    "Cache hit for {FileName} (hash: {Hash})",
                    Path.GetFileName(filePath),
                    contentHash
                );
                return new CompilationResult
                {
                    Success = true,
                    CompiledType = cached.CompiledType,
                    Errors = new List<string>(),
                    Diagnostics = new List<CompilationDiagnostic>(),
                };
            }

            _logger.LogDebug(
                "Cache miss for {FileName} (hash: {Hash}), compiling...",
                Path.GetFileName(filePath),
                contentHash
            );

            // Prepare script with global usings
            string fullScript = PrepareScriptWithUsings(scriptContent);

            // Parse the script
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                fullScript,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                filePath
            );

            // Create compilation
            string assemblyName =
                $"Script_{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                _metadataReferences,
                _compilationOptions
            );

            // Emit to memory stream
            using var ms = new MemoryStream();
            EmitResult emitResult = compilation.Emit(ms);

            // Process diagnostics
            List<CompilationDiagnostic> diagnostics = ProcessDiagnostics(emitResult.Diagnostics);

            if (!emitResult.Success)
            {
                _logger.LogError(
                    "Compilation failed for {FileName} with {ErrorCount} errors",
                    Path.GetFileName(filePath),
                    diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)
                );

                return new CompilationResult
                {
                    Success = false,
                    CompiledType = null,
                    Errors = diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.Message)
                        .ToList(),
                    Diagnostics = diagnostics,
                };
            }

            // Load assembly and extract type
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            // Find the first public class that inherits from TypeScriptBase
            Type? compiledType = FindScriptType(assembly);

            if (compiledType == null)
            {
                _logger.LogError(
                    "No valid TypeScriptBase-derived class found in {FileName}",
                    Path.GetFileName(filePath)
                );
                return new CompilationResult
                {
                    Success = false,
                    CompiledType = null,
                    Errors = new List<string>
                    {
                        "No public class inheriting from TypeScriptBase found in script",
                    },
                    Diagnostics = diagnostics,
                };
            }

            // Cache the compiled result
            var cachedCompilation = new CachedCompilation
            {
                CompiledType = compiledType,
                ContentHash = contentHash,
                CompiledAt = DateTime.UtcNow,
            };
            _compilationCache[contentHash] = cachedCompilation;

            _logger.LogInformation(
                "Successfully compiled {FileName} -> {TypeName} (cached by hash: {Hash})",
                Path.GetFileName(filePath),
                compiledType.Name,
                contentHash
            );

            return new CompilationResult
            {
                Success = true,
                CompiledType = compiledType,
                Errors = new List<string>(),
                Diagnostics = diagnostics,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error compiling script: {FilePath}", filePath);
            return new CompilationResult
            {
                Success = false,
                CompiledType = null,
                Errors = new List<string> { $"Compilation exception: {ex.Message}" },
                Diagnostics = new List<CompilationDiagnostic>(),
            };
        }
    }

    /// <summary>
    ///     Compute SHA256 hash of script content for cache key.
    /// </summary>
    private static string ComputeContentHash(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    ///     Prepare script with global usings prepended.
    /// </summary>
    private string PrepareScriptWithUsings(string scriptContent)
    {
        var sb = new StringBuilder();

        // Add global usings
        foreach (string globalUsing in _globalUsings)
        {
            sb.AppendLine($"using {globalUsing};");
        }

        sb.AppendLine(); // Blank line for readability
        sb.Append(scriptContent);

        return sb.ToString();
    }

    /// <summary>
    ///     Process Roslyn diagnostics into CompilationDiagnostic objects.
    /// </summary>
    private List<CompilationDiagnostic> ProcessDiagnostics(
        IEnumerable<Diagnostic> roslynDiagnostics
    )
    {
        var diagnostics = new List<CompilationDiagnostic>();

        foreach (Diagnostic diagnostic in roslynDiagnostics)
        {
            FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();
            diagnostics.Add(
                new CompilationDiagnostic
                {
                    Severity = MapSeverity(diagnostic.Severity),
                    Message = diagnostic.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1, // 1-based line numbers
                    Column = lineSpan.StartLinePosition.Character + 1, // 1-based columns
                    Code = diagnostic.Id,
                    FilePath = lineSpan.Path,
                }
            );
        }

        return diagnostics;
    }

    /// <summary>
    ///     Map Roslyn DiagnosticSeverity to our DiagnosticSeverity enum.
    /// </summary>
    private static DiagnosticSeverity MapSeverity(
        Microsoft.CodeAnalysis.DiagnosticSeverity roslynSeverity
    )
    {
        return roslynSeverity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden => DiagnosticSeverity.Hidden,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Info => DiagnosticSeverity.Info,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
            _ => DiagnosticSeverity.Hidden,
        };
    }

    /// <summary>
    ///     Find the first public class that inherits from TypeScriptBase.
    /// </summary>
    private Type? FindScriptType(Assembly assembly)
    {
        try
        {
            Type baseType = typeof(TypeScriptBase);
            return assembly
                .GetTypes()
                .FirstOrDefault(t =>
                    t.IsClass && !t.IsAbstract && t.IsPublic && baseType.IsAssignableFrom(t)
                );
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogError(
                ex,
                "ReflectionTypeLoadException while searching for script type in assembly"
            );
            foreach (Exception? loaderException in ex.LoaderExceptions)
            {
                if (loaderException != null)
                {
                    _logger.LogError("Loader exception: {Message}", loaderException.Message);
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     Get default metadata references matching ScriptCompilationOptions.
    /// </summary>
    private List<MetadataReference> GetDefaultMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // System.Private.CoreLib
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location), // System.Linq
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location), // System.Collections
            MetadataReference.CreateFromFile(typeof(World).Assembly.Location), // Arch.Core
            MetadataReference.CreateFromFile(typeof(Point).Assembly.Location), // MonoGame.Framework
            MetadataReference.CreateFromFile(typeof(TypeScriptBase).Assembly.Location), // PokeSharp.Scripting
            MetadataReference.CreateFromFile(typeof(Direction).Assembly.Location), // PokeSharp.Core
            MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location), // Microsoft.Extensions.Logging.Abstractions
        };

        // Add runtime references for .NET 9
        string? runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrEmpty(runtimePath))
        {
            string[] runtimeRefs = new[]
            {
                "System.Runtime.dll",
                "System.Collections.dll",
                "System.Linq.dll",
                "netstandard.dll",
            };

            foreach (string runtimeRef in runtimeRefs)
            {
                string refPath = Path.Combine(runtimePath, runtimeRef);
                if (File.Exists(refPath))
                {
                    references.Add(MetadataReference.CreateFromFile(refPath));
                }
            }
        }

        return references;
    }

    /// <summary>
    ///     Get default global usings matching ScriptCompilationOptions.
    /// </summary>
    private List<string> GetDefaultGlobalUsings()
    {
        return new List<string>
        {
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "Arch.Core",
            "Microsoft.Xna.Framework",
            "Microsoft.Extensions.Logging",
            "PokeSharp.Scripting.Runtime",
            "PokeSharp.Core.ScriptingApi",
            "PokeSharp.Core.Components.Maps",
            "PokeSharp.Core.Components.Movement",
            "PokeSharp.Core.Components.NPCs",
            "PokeSharp.Core.Components.NPCs.States",
            "PokeSharp.Core.Components.Player",
            "PokeSharp.Core.Components.Rendering",
            "PokeSharp.Core.Components.Tiles",
            "PokeSharp.Core.Types",
        };
    }

    /// <summary>
    ///     Clear the compilation cache.
    /// </summary>
    public void ClearCache()
    {
        _compilationCache.Clear();
        _logger.LogInformation("Cleared compilation cache");
    }

    /// <summary>
    ///     Get cache statistics.
    /// </summary>
    public CompilationCacheStatistics GetCacheStatistics()
    {
        return new CompilationCacheStatistics
        {
            CachedEntries = _compilationCache.Count,
            TotalSize = _compilationCache.Sum(kvp =>
                kvp.Value.CompiledType?.Assembly.GetName().Name?.Length ?? 0
            ),
        };
    }
}
