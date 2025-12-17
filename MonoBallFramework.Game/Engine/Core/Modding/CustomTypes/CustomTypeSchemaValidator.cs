using System.Collections.Concurrent;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;

/// <summary>
///     Validates custom type definitions against JSON Schemas.
///     Provides clear error messages for modders when their definitions don't match the schema.
/// </summary>
public sealed class CustomTypeSchemaValidator : IDisposable
{
    private readonly ILogger<CustomTypeSchemaValidator> _logger;
    private readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new();

    public CustomTypeSchemaValidator(ILogger<CustomTypeSchemaValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void Dispose()
    {
        _schemaCache.Clear();
    }

    /// <summary>
    ///     Loads and caches a JSON Schema from a file path.
    /// </summary>
    /// <param name="schemaPath">Absolute path to the schema file.</param>
    /// <returns>The parsed schema, or null if loading failed.</returns>
    public JsonSchema? LoadSchema(string schemaPath)
    {
        return _schemaCache.GetOrAdd(schemaPath, path =>
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("Schema file not found: {Path}", path);
                return null!;
            }

            try
            {
                string schemaJson = File.ReadAllText(path);
                var schema = JsonSchema.FromText(schemaJson);
                _logger.LogDebug("Loaded schema from {Path}", path);
                return schema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse schema at {Path}", path);
                return null!;
            }
        });
    }

    /// <summary>
    ///     Validates a JSON document against a schema.
    /// </summary>
    /// <param name="schema">The schema to validate against.</param>
    /// <param name="document">The JSON document to validate.</param>
    /// <param name="filePath">The file path (for error messages).</param>
    /// <returns>A validation result with success status and any errors.</returns>
    public SchemaValidationResult Validate(JsonSchema schema, JsonDocument document, string filePath)
    {
        try
        {
            var evaluationOptions = new EvaluationOptions { OutputFormat = OutputFormat.List };

            EvaluationResults result = schema.Evaluate(document.RootElement, evaluationOptions);

            if (result.IsValid)
            {
                return SchemaValidationResult.Success();
            }

            var errors = new List<SchemaValidationError>();
            CollectErrors(result, errors);

            return SchemaValidationResult.Failed(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation threw exception for {Path}", filePath);
            return SchemaValidationResult.Failed(new List<SchemaValidationError>
            {
                new("", $"Validation error: {ex.Message}")
            });
        }
    }

    /// <summary>
    ///     Validates a JSON file against a schema file.
    /// </summary>
    /// <param name="schemaPath">Path to the schema file.</param>
    /// <param name="definitionPath">Path to the definition file to validate.</param>
    /// <returns>A validation result with success status and any errors.</returns>
    public SchemaValidationResult ValidateFile(string schemaPath, string definitionPath)
    {
        JsonSchema? schema = LoadSchema(schemaPath);
        if (schema == null)
        {
            return SchemaValidationResult.Failed(new List<SchemaValidationError>
            {
                new("", $"Could not load schema from {schemaPath}")
            });
        }

        try
        {
            string json = File.ReadAllText(definitionPath);
            using var document = JsonDocument.Parse(json);
            return Validate(schema, document, definitionPath);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.Failed(new List<SchemaValidationError>
            {
                new("", $"Invalid JSON: {ex.Message}")
            });
        }
    }

    private void CollectErrors(EvaluationResults result, List<SchemaValidationError> errors)
    {
        if (!result.IsValid && result.Errors != null)
        {
            foreach (KeyValuePair<string, string> error in result.Errors)
            {
                string path = result.InstanceLocation?.ToString() ?? "";
                errors.Add(new SchemaValidationError(path, $"{error.Key}: {error.Value}"));
            }
        }

        if (result.Details != null)
        {
            foreach (EvaluationResults detail in result.Details)
            {
                CollectErrors(detail, errors);
            }
        }
    }

    /// <summary>
    ///     Clears the schema cache (useful for hot reload).
    /// </summary>
    public void ClearCache()
    {
        _schemaCache.Clear();
    }
}

/// <summary>
///     Result of schema validation.
/// </summary>
public sealed class SchemaValidationResult
{
    private SchemaValidationResult(bool isValid, IReadOnlyList<SchemaValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public bool IsValid { get; }
    public IReadOnlyList<SchemaValidationError> Errors { get; }

    public static SchemaValidationResult Success()
    {
        return new SchemaValidationResult(true, Array.Empty<SchemaValidationError>());
    }

    public static SchemaValidationResult Failed(IReadOnlyList<SchemaValidationError> errors)
    {
        return new SchemaValidationResult(false, errors);
    }

    /// <summary>
    ///     Formats all errors as a single string for logging.
    /// </summary>
    public string FormatErrors()
    {
        if (Errors.Count == 0)
        {
            return "No errors";
        }

        return string.Join(Environment.NewLine, Errors.Select(e =>
            string.IsNullOrEmpty(e.Path) ? e.Message : $"  {e.Path}: {e.Message}"));
    }
}

/// <summary>
///     A single schema validation error.
/// </summary>
public sealed record SchemaValidationError(string Path, string Message);
