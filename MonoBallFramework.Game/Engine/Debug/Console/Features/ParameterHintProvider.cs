using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace MonoBallFramework.Game.Engine.Debug.Console.Features;

/// <summary>
///     Provides parameter hints for method calls, showing signatures and highlighting current parameter.
/// </summary>
public class ParameterHintProvider
{
    // Regex to detect method call pattern: methodName(
    private static readonly Regex MethodCallRegex = new(@"(\w+)\($", RegexOptions.Compiled);

    // Regex to detect member method call: object.methodName(
    private static readonly Regex MemberMethodCallRegex = new(
        @"(\w+)\.(\w+)\($",
        RegexOptions.Compiled
    );

    private readonly ILogger? _logger;
    private object? _globalsInstance;
    private List<string>? _importedNamespaces;
    private List<Assembly>? _referencedAssemblies;
    private ScriptState<object>? _scriptState;

    public ParameterHintProvider(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Set the globals instance to enable member completion
    /// </summary>
    public void SetGlobals(object globals)
    {
        _globalsInstance = globals;
    }

    /// <summary>
    ///     Update the script state after execution to track variables
    /// </summary>
    public void UpdateScriptState(ScriptState<object>? state)
    {
        _scriptState = state;
    }

    /// <summary>
    ///     Set the referenced assemblies and imported namespaces
    /// </summary>
    public void SetReferences(IEnumerable<Assembly> assemblies, IEnumerable<string> namespaces)
    {
        _referencedAssemblies = assemblies.ToList();
        _importedNamespaces = namespaces.ToList();
    }

    /// <summary>
    ///     Gets parameter hints for the method at the cursor position.
    ///     Returns null if no method call is detected.
    /// </summary>
    public ParameterHintInfo? GetParameterHints(string code, int cursorPosition)
    {
        try
        {
            if (cursorPosition < 0 || cursorPosition > code.Length)
            {
                return null;
            }

            string textUpToCursor = code.Substring(0, cursorPosition);

            // Check for member method call (object.Method()
            Match memberMatch = MemberMethodCallRegex.Match(textUpToCursor);
            if (memberMatch.Success)
            {
                string objectName = memberMatch.Groups[1].Value;
                string methodName = memberMatch.Groups[2].Value;
                _logger?.LogDebug(
                    "Detected member method call: {Object}.{Method}",
                    objectName,
                    methodName
                );
                return GetMemberMethodHints(objectName, methodName);
            }

            // Check for direct method call (Method()
            Match methodMatch = MethodCallRegex.Match(textUpToCursor);
            if (methodMatch.Success)
            {
                string methodName = methodMatch.Groups[1].Value;
                _logger?.LogDebug("Detected direct method call: {Method}", methodName);
                return GetDirectMethodHints(methodName);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting parameter hints");
            return null;
        }
    }

    /// <summary>
    ///     Gets hints for a member method (e.g., Console.WriteLine)
    /// </summary>
    private ParameterHintInfo? GetMemberMethodHints(string objectName, string methodName)
    {
        Type? targetType = null;

        // Try to find object in script state
        if (_scriptState != null)
        {
            ScriptVariable? variable = _scriptState.Variables.FirstOrDefault(v =>
                string.Equals(v.Name, objectName, StringComparison.OrdinalIgnoreCase)
            );
            if (variable != null)
            {
                targetType = variable.Type;
            }
        }

        // Try to find in globals
        if (targetType == null && _globalsInstance != null)
        {
            Type globalsType = _globalsInstance.GetType();
            PropertyInfo? property = globalsType.GetProperty(
                objectName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
            );
            FieldInfo? field = globalsType.GetField(
                objectName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
            );

            if (property != null)
            {
                targetType = property.PropertyType;
            }
            else if (field != null)
            {
                targetType = field.FieldType;
            }
        }

        // Try to find as a type name
        if (targetType == null)
        {
            targetType = FindTypeByName(objectName);
        }

        if (targetType == null)
        {
            return null;
        }

        // Get method overloads
        var methods = targetType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (methods.Count == 0)
        {
            return null;
        }

        return new ParameterHintInfo
        {
            MethodName = methodName,
            Overloads = methods.Select(CreateMethodSignature).ToList(),
            CurrentOverloadIndex = 0,
        };
    }

    /// <summary>
    ///     Gets hints for a direct method call (globals or imported)
    /// </summary>
    private ParameterHintInfo? GetDirectMethodHints(string methodName)
    {
        var methods = new List<MethodInfo>();

        // Check globals for methods
        if (_globalsInstance != null)
        {
            Type globalsType = _globalsInstance.GetType();
            IEnumerable<MethodInfo> globalMethods = globalsType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
            methods.AddRange(globalMethods);
        }

        if (methods.Count == 0)
        {
            return null;
        }

        return new ParameterHintInfo
        {
            MethodName = methodName,
            Overloads = methods.Select(CreateMethodSignature).ToList(),
            CurrentOverloadIndex = 0,
        };
    }

    /// <summary>
    ///     Creates a formatted method signature string
    /// </summary>
    private MethodSignature CreateMethodSignature(MethodInfo method)
    {
        var parameters = method
            .GetParameters()
            .Select(p => new ParameterInfo
            {
                Name = p.Name ?? "arg",
                Type = GetFriendlyTypeName(p.ParameterType),
                IsOptional = p.IsOptional,
                DefaultValue = p.IsOptional ? p.DefaultValue?.ToString() : null,
            })
            .ToList();

        return new MethodSignature
        {
            MethodName = method.Name,
            ReturnType = GetFriendlyTypeName(method.ReturnType),
            Parameters = parameters,
        };
    }

    /// <summary>
    ///     Find a type by name from referenced assemblies
    /// </summary>
    private Type? FindTypeByName(string typeName)
    {
        if (_referencedAssemblies == null || _importedNamespaces == null)
        {
            return null;
        }

        foreach (Assembly assembly in _referencedAssemblies)
        {
            try
            {
                Type[] types = assembly.GetExportedTypes();
                Type? match = types.FirstOrDefault(t =>
                    t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                    && t.IsPublic
                    && _importedNamespaces.Any(ns =>
                        t.Namespace != null
                        && (t.Namespace == ns || t.Namespace.StartsWith(ns + "."))
                    )
                );

                if (match != null)
                {
                    return match;
                }
            }
            catch
            {
                // Ignore assembly load errors
            }
        }

        return null;
    }

    /// <summary>
    ///     Converts a .NET Type to a friendly C# display name
    /// </summary>
    private string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type? underlyingType = Nullable.GetUnderlyingType(type);
            return underlyingType != null ? $"{GetFriendlyTypeName(underlyingType)}?" : type.Name;
        }

        if (type.IsArray)
        {
            Type? elementType = type.GetElementType();
            return $"{GetFriendlyTypeName(elementType!)}[]";
        }

        if (type.IsGenericType)
        {
            Type genericType = type.GetGenericTypeDefinition();
            Type[] genericArgs = type.GetGenericArguments();
            string genericTypeName = genericType.Name.Substring(0, genericType.Name.IndexOf('`'));
            string genericArgsNames = string.Join(", ", genericArgs.Select(GetFriendlyTypeName));
            return $"{genericTypeName}<{genericArgsNames}>";
        }

        return type.Name switch
        {
            "Int32" => "int",
            "String" => "string",
            "Boolean" => "bool",
            "Single" => "float",
            "Double" => "double",
            "Void" => "void",
            _ => type.Name,
        };
    }
}

/// <summary>
///     Contains parameter hint information for a method call
/// </summary>
public class ParameterHintInfo
{
    public string MethodName { get; set; } = "";
    public List<MethodSignature> Overloads { get; set; } = new();
    public int CurrentOverloadIndex { get; set; }
}

/// <summary>
///     Represents a method signature with parameters
/// </summary>
public class MethodSignature
{
    public string MethodName { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public List<ParameterInfo> Parameters { get; set; } = new();

    public string GetSignature()
    {
        IEnumerable<string> paramStrings = Parameters.Select(p =>
        {
            string paramStr = $"{p.Type} {p.Name}";
            if (p.IsOptional && p.DefaultValue != null)
            {
                paramStr += $" = {p.DefaultValue}";
            }

            return paramStr;
        });

        return $"{ReturnType} {MethodName}({string.Join(", ", paramStrings)})";
    }
}

/// <summary>
///     Represents a method parameter
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}
