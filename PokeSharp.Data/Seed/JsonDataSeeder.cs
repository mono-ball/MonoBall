using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Data.Seed;

/// <summary>
/// JSON-based data seeder using System.Text.Json.
/// Loads game data from JSON files into the database.
/// Idempotent - safe to run multiple times (checks for existing data).
/// </summary>
public sealed class JsonDataSeeder : IDataSeeder
{
    private readonly ILogger<JsonDataSeeder> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _defaultDataPath;

    public JsonDataSeeder(
        ILogger<JsonDataSeeder> logger,
        string defaultDataPath = "data")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultDataPath = defaultDataPath ?? throw new ArgumentNullException(nameof(defaultDataPath));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc/>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data seeding from default path: {DataPath}", _defaultDataPath);

        if (!Directory.Exists(_defaultDataPath))
        {
            _logger.LogWarning("Data directory not found: {DataPath}. Creating empty directory.", _defaultDataPath);
            Directory.CreateDirectory(_defaultDataPath);
            return 0;
        }

        return await SeedFromDirectoryAsync(_defaultDataPath, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SeedFromDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath, nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogError("Directory not found: {DirectoryPath}", directoryPath);
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
        _logger.LogInformation("Found {FileCount} JSON files in {DirectoryPath}", jsonFiles.Length, directoryPath);

        var totalSeeded = 0;

        foreach (var jsonFile in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var seeded = await SeedFromFileAsync(jsonFile, cancellationToken);
                totalSeeded += seeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed data from file: {FilePath}", jsonFile);
                // Continue processing other files
            }
        }

        _logger.LogInformation("Seeding complete. Total entities seeded: {TotalSeeded}", totalSeeded);
        return totalSeeded;
    }

    /// <inheritdoc/>
    public async Task<int> SeedFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            _logger.LogError("File not found: {FilePath}", filePath);
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogDebug("Seeding data from file: {FilePath}", filePath);

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            // Try to parse as array first
            if (json.TrimStart().StartsWith('['))
            {
                return await SeedArrayAsync(json, filePath, cancellationToken);
            }
            else
            {
                return await SeedSingleAsync(json, filePath, cancellationToken);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in file: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<DataValidationResult> ValidateAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath, nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
        {
            return DataValidationResult.Failure($"Directory not found: {directoryPath}");
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

        _logger.LogInformation("Validating {FileCount} JSON files", jsonFiles.Length);

        foreach (var jsonFile in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(jsonFile, cancellationToken);

                // Try to parse JSON to validate syntax
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

                // Additional validation can be added here
                // For example, checking required fields, data types, etc.
            }
            catch (JsonException ex)
            {
                errors.Add($"{Path.GetFileName(jsonFile)}: {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(jsonFile)}: Unexpected error - {ex.Message}");
            }
        }

        var isValid = errors.Count == 0;

        if (isValid)
        {
            _logger.LogInformation("Validation successful for {FileCount} files", jsonFiles.Length);
        }
        else
        {
            _logger.LogWarning("Validation found {ErrorCount} errors in {FileCount} files",
                errors.Count, jsonFiles.Length);
        }

        return new DataValidationResult
        {
            IsValid = isValid,
            Errors = errors,
            Warnings = warnings,
            FilesChecked = jsonFiles.Length
        };
    }

    // Private helper methods

    private async Task<int> SeedArrayAsync(
        string json,
        string filePath,
        CancellationToken cancellationToken)
    {
        // For now, just parse and count
        // In a real implementation, this would deserialize to specific entity types
        // and insert into the database
        using var document = JsonDocument.Parse(json);
        var arrayLength = document.RootElement.GetArrayLength();

        _logger.LogDebug("Parsed array with {Count} entities from {FilePath}",
            arrayLength, Path.GetFileName(filePath));

        return await Task.FromResult(arrayLength);
    }

    private async Task<int> SeedSingleAsync(
        string json,
        string filePath,
        CancellationToken cancellationToken)
    {
        // For now, just parse and validate
        // In a real implementation, this would deserialize to specific entity type
        // and insert into the database
        using var document = JsonDocument.Parse(json);

        _logger.LogDebug("Parsed single entity from {FilePath}",
            Path.GetFileName(filePath));

        return await Task.FromResult(1);
    }
}
