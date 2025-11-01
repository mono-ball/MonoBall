namespace PokeSharp.Data.Seed;

/// <summary>
/// Interface for seeding game data from external sources (JSON, YAML, etc.).
/// Implementations should be idempotent (safe to run multiple times).
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seed all data from configured data sources.
    /// This method should check if data already exists before seeding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities seeded</returns>
    Task<int> SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seed data from a specific directory.
    /// Scans directory recursively for data files and loads them.
    /// </summary>
    /// <param name="directoryPath">Absolute path to directory containing data files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities seeded</returns>
    Task<int> SeedFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seed data from a single file.
    /// </summary>
    /// <param name="filePath">Absolute path to data file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities seeded from this file</returns>
    Task<int> SeedFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate data files without seeding.
    /// Useful for CI/CD validation and development.
    /// </summary>
    /// <param name="directoryPath">Directory to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with errors</returns>
    Task<DataValidationResult> ValidateAsync(string directoryPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of data validation.
/// </summary>
public sealed class DataValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public int FilesChecked { get; init; }

    public static DataValidationResult Success(int filesChecked) => new()
    {
        IsValid = true,
        FilesChecked = filesChecked
    };

    public static DataValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}
