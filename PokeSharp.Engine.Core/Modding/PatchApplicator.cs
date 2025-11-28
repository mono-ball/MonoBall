using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
///     Applies JSON Patch operations to JSON documents.
///     Implements RFC 6902: https://datatracker.ietf.org/doc/html/rfc6902
/// </summary>
public sealed class PatchApplicator
{
    private readonly ILogger<PatchApplicator> _logger;

    public PatchApplicator(ILogger<PatchApplicator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Applies a patch to a JSON document
    /// </summary>
    public JsonNode? ApplyPatch(JsonNode? document, ModPatch patch)
    {
        if (document == null)
        {
            _logger.LogWarning(
                "[steelblue1]WF[/] [orange3]⚠[/] Cannot apply patch to null document: [cyan]{Target}[/]",
                patch.Target
            );
            return null;
        }

        JsonNode? current = document.Deserialize<JsonNode>();
        if (current == null)
        {
            _logger.LogWarning(
                "[steelblue1]WF[/] [orange3]⚠[/] Failed to deserialize document: [cyan]{Target}[/]",
                patch.Target
            );
            return document;
        }

        foreach (PatchOperation operation in patch.Operations)
        {
            try
            {
                operation.Validate();
                current = ApplyOperation(current, operation);
                if (current == null)
                {
                    _logger.LogWarning(
                        "[steelblue1]WF[/] [orange3]⚠[/] Operation [cyan]{Op}[/] [cyan]{Path}[/] resulted in null document",
                        operation.Op,
                        operation.Path
                    );
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[steelblue1]WF[/] [red]✗[/] Failed to apply patch operation [cyan]{Op}[/] [cyan]{Path}[/] to [cyan]{Target}[/]",
                    operation.Op,
                    operation.Path,
                    patch.Target
                );
                throw;
            }
        }

        return current;
    }

    private JsonNode? ApplyOperation(JsonNode document, PatchOperation operation)
    {
        switch (operation.Op.ToLowerInvariant())
        {
            case "add":
                return ApplyAdd(document, operation.Path, operation.Value!.Value);

            case "remove":
                return ApplyRemove(document, operation.Path);

            case "replace":
                return ApplyReplace(document, operation.Path, operation.Value!.Value);

            case "move":
                return ApplyMove(document, operation.From!, operation.Path);

            case "copy":
                return ApplyCopy(document, operation.From!, operation.Path);

            case "test":
                ApplyTest(document, operation.Path, operation.Value!.Value);
                return document;

            default:
                throw new InvalidOperationException($"Unknown operation: {operation.Op}");
        }
    }

    private JsonNode? ApplyAdd(JsonNode document, string path, JsonElement value)
    {
        (var parent, string key) = NavigateToParent(document, path);
        var jsonValue = JsonNode.Parse(value.GetRawText());

        if (parent is JsonObject obj)
        {
            obj[key] = jsonValue;
        }
        else if (parent is JsonArray arr)
        {
            if (key == "-")
            // Append to end of array
            {
                arr.Add(jsonValue);
            }
            else if (int.TryParse(key, out int index))
            {
                arr.Insert(index, jsonValue);
            }
            else
            {
                throw new InvalidOperationException($"Invalid array index: {key}");
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot add to {parent?.GetType().Name ?? "null"}"
            );
        }

        return document;
    }

    private JsonNode? ApplyRemove(JsonNode document, string path)
    {
        (var parent, string key) = NavigateToParent(document, path);

        if (parent is JsonObject obj)
        {
            obj.Remove(key);
        }
        else if (parent is JsonArray arr && int.TryParse(key, out int index))
        {
            arr.RemoveAt(index);
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot remove from {parent?.GetType().Name ?? "null"}"
            );
        }

        return document;
    }

    private JsonNode? ApplyReplace(JsonNode document, string path, JsonElement value)
    {
        // Replace is equivalent to remove + add (but target must exist)
        (var parent, string key) = NavigateToParent(document, path);
        var jsonValue = JsonNode.Parse(value.GetRawText());

        if (parent is JsonObject obj)
        {
            if (!obj.ContainsKey(key))
            {
                throw new InvalidOperationException($"Path does not exist: {path}");
            }

            obj[key] = jsonValue;
        }
        else if (parent is JsonArray arr && int.TryParse(key, out int index))
        {
            if (index < 0 || index >= arr.Count)
            {
                throw new InvalidOperationException($"Array index out of range: {index}");
            }

            arr[index] = jsonValue;
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot replace in {parent?.GetType().Name ?? "null"}"
            );
        }

        return document;
    }

    private JsonNode? ApplyMove(JsonNode document, string from, string to)
    {
        // Get value from source
        JsonNode? value = NavigateToValue(document, from);
        if (value == null)
        {
            throw new InvalidOperationException($"Cannot move from null value at: {from}");
        }

        // Remove from source
        JsonNode? afterRemove = ApplyRemove(document, from);
        if (afterRemove == null)
        {
            throw new InvalidOperationException($"Document became null after removing: {from}");
        }

        // Add to destination
        JsonElement jsonElement = JsonSerializer.SerializeToElement(value);
        JsonNode? afterAdd = ApplyAdd(afterRemove, to, jsonElement);

        return afterAdd;
    }

    private JsonNode? ApplyCopy(JsonNode document, string from, string to)
    {
        // Get value from source (don't remove)
        JsonNode? value = NavigateToValue(document, from);
        if (value == null)
        {
            throw new InvalidOperationException($"Cannot copy from null value at: {from}");
        }

        // Add to destination
        JsonElement jsonElement = JsonSerializer.SerializeToElement(value);
        JsonNode? afterAdd = ApplyAdd(document, to, jsonElement);

        return afterAdd;
    }

    private void ApplyTest(JsonNode document, string path, JsonElement expectedValue)
    {
        JsonNode? actualValue = NavigateToValue(document, path);
        JsonElement actualElement = JsonSerializer.SerializeToElement(actualValue);

        // Compare JSON representations
        string actualJson = actualElement.GetRawText();
        string expectedJson = expectedValue.GetRawText();

        if (actualJson != expectedJson)
        {
            throw new InvalidOperationException(
                $"Test failed at {path}: expected {expectedJson} but got {actualJson}"
            );
        }
    }

    /// <summary>
    ///     Navigates to the parent of the target path and returns (parent, key)
    /// </summary>
    private (JsonNode parent, string key) NavigateToParent(JsonNode document, string path)
    {
        List<string> segments = ParsePath(path);

        if (segments.Count == 0)
        {
            throw new InvalidOperationException("Cannot navigate to parent of root");
        }

        JsonNode? current = document;

        // Navigate to parent (all segments except last)
        for (int i = 0; i < segments.Count - 1; i++)
        {
            string segment = segments[i];

            if (current is JsonObject obj)
            {
                current = obj[segment];
            }
            else if (current is JsonArray arr && int.TryParse(segment, out int index))
            {
                current = arr[index];
            }
            else
            {
                throw new InvalidOperationException($"Cannot navigate path: {path}");
            }

            if (current == null)
            {
                throw new InvalidOperationException($"Path not found: {path}");
            }
        }

        return (current, segments[^1]);
    }

    /// <summary>
    ///     Navigates to the value at the target path
    /// </summary>
    private JsonNode? NavigateToValue(JsonNode document, string path)
    {
        List<string> segments = ParsePath(path);
        JsonNode? current = document;

        foreach (string segment in segments)
        {
            if (current is JsonObject obj)
            {
                current = obj[segment];
            }
            else if (current is JsonArray arr && int.TryParse(segment, out int index))
            {
                current = arr[index];
            }
            else
            {
                throw new InvalidOperationException($"Cannot navigate path: {path}");
            }

            if (current == null)
            {
                throw new InvalidOperationException($"Path not found: {path}");
            }
        }

        return current;
    }

    /// <summary>
    ///     Parses a JSON Pointer path into segments
    /// </summary>
    private List<string> ParsePath(string path)
    {
        if (path == "/")
        {
            return new List<string>();
        }

        // Remove leading slash and split
        string[] segments = path.TrimStart('/').Split('/');

        // Unescape special characters (~0 = ~, ~1 = /)
        return segments.Select(s => s.Replace("~1", "/").Replace("~0", "~")).ToList();
    }
}
