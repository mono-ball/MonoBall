using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Utilities;

/// <summary>
///     Utility class for common file operations with consistent error handling.
///     Eliminates code duplication across file handling classes.
/// </summary>
public static class FileUtilities
{
    /// <summary>
    ///     Safely reads a text file with error handling and logging.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>File contents, or null if the file doesn't exist or an error occurs.</returns>
    public static string? ReadTextFile(string filePath, ILogger? logger = null)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to read file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    ///     Safely writes text to a file with error handling and logging.
    /// </summary>
    /// <param name="filePath">Path to the file to write.</param>
    /// <param name="content">Content to write.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool WriteTextFile(string filePath, string content, ILogger? logger = null)
    {
        try
        {
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to write file: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    ///     Safely reads and deserializes a JSON file with error handling and logging.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>Deserialized object, or default(T) if file doesn't exist or an error occurs.</returns>
    public static T? ReadJsonFile<T>(string filePath, ILogger? logger = null)
        where T : class
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to read JSON file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    ///     Safely serializes and writes an object to a JSON file with error handling and logging.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <param name="data">Object to serialize.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool WriteJsonFile<T>(string filePath, T data, ILogger? logger = null)
        where T : class
    {
        try
        {
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to write JSON file: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    ///     Safely deletes a file with error handling and logging.
    /// </summary>
    /// <param name="filePath">Path to the file to delete.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>True if successful or file doesn't exist, false if an error occurs.</returns>
    public static bool DeleteFile(string filePath, ILogger? logger = null)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return true; // Consider non-existent file as success
            }

            File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete file: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    ///     Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directoryPath">Path to the directory.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool EnsureDirectoryExists(string directoryPath, ILogger? logger = null)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create directory: {DirectoryPath}", directoryPath);
            return false;
        }
    }
}
