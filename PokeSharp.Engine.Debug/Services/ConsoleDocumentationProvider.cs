using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.UI.Debug.Components.Controls;

namespace PokeSharp.Engine.Debug.Services;

/// <summary>
///     Provides comprehensive documentation for console symbols using reflection.
/// </summary>
public class ConsoleDocumentationProvider
{
    private readonly ILogger _logger;
    private ConsoleScriptEvaluator? _evaluator;
    private ConsoleGlobals? _globals;
    private List<Assembly>? _referencedAssemblies;

    public ConsoleDocumentationProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Sets the console globals for documentation lookup.
    /// </summary>
    public void SetGlobals(ConsoleGlobals globals)
    {
        _globals = globals;
    }

    /// <summary>
    ///     Sets the script evaluator for accessing script state.
    /// </summary>
    public void SetEvaluator(ConsoleScriptEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    /// <summary>
    ///     Sets the referenced assemblies for type lookup.
    /// </summary>
    public void SetReferencedAssemblies(List<Assembly> assemblies)
    {
        _referencedAssemblies = assemblies;
    }

    /// <summary>
    ///     Generates documentation for the given symbol.
    /// </summary>
    public DocInfo GetDocumentation(string symbolName)
    {
        try
        {
            return GenerateDocumentation(symbolName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating documentation for symbol: {Symbol}", symbolName);

            return new DocInfo
            {
                Title = symbolName,
                Summary = "Error retrieving documentation.",
                Remarks = ex.Message,
            };
        }
    }

    /// <summary>
    ///     Generates comprehensive documentation for a symbol using reflection.
    /// </summary>
    private DocInfo GenerateDocumentation(string symbolName)
    {
        var doc = new DocInfo { Title = symbolName };

        if (_globals == null)
        {
            doc.Summary = "Console globals not initialized.";
            return doc;
        }

        // Try globals members first
        Type globalsType = _globals.GetType();

        // Check properties
        PropertyInfo? prop = globalsType.GetProperty(symbolName);
        if (prop != null)
        {
            doc.Summary = "Property of console globals.";
            doc.Signature = $"{FormatTypeName(prop.PropertyType)} {symbolName} {{ get; }}";
            doc.ReturnType = FormatTypeName(prop.PropertyType);

            // Try to get the value and show it
            try
            {
                object? value = prop.GetValue(_globals);
                if (value != null)
                {
                    doc.Remarks = $"Current value: {value}";
                }
            }
            catch { }

            return doc;
        }

        // Check fields
        FieldInfo? field = globalsType.GetField(symbolName);
        if (field != null)
        {
            doc.Summary = "Field of console globals.";
            doc.Signature = $"{FormatTypeName(field.FieldType)} {symbolName}";
            doc.ReturnType = FormatTypeName(field.FieldType);

            try
            {
                object? value = field.GetValue(_globals);
                if (value != null)
                {
                    doc.Remarks = $"Current value: {value}";
                }
            }
            catch { }

            return doc;
        }

        // Check methods
        var methods = globalsType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == symbolName && !m.IsSpecialName)
            .ToList();

        if (methods.Count > 0)
        {
            MethodInfo method = methods[0]; // Show first overload
            doc.Summary =
                methods.Count > 1
                    ? $"Method of console globals. ({methods.Count} overload{(methods.Count > 1 ? "s" : "")})"
                    : "Method of console globals.";

            ParameterInfo[] parameters = method.GetParameters();
            IEnumerable<string> paramStrings = parameters.Select(p =>
                $"{FormatTypeName(p.ParameterType)} {p.Name}{(p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : "")}"
            );

            doc.Signature =
                $"{FormatTypeName(method.ReturnType)} {symbolName}({string.Join(", ", paramStrings)})";
            doc.ReturnType = FormatTypeName(method.ReturnType);

            if (parameters.Length > 0)
            {
                doc.Parameters = parameters
                    .Select(p => new ParamDoc
                    {
                        Name = p.Name ?? "param",
                        Description =
                            $"{FormatTypeName(p.ParameterType)}{(p.HasDefaultValue ? $" (default: {p.DefaultValue ?? "null"})" : "")}",
                    })
                    .ToList();
            }

            if (methods.Count > 1)
            {
                doc.Remarks =
                    $"This method has {methods.Count} overloads. Press Ctrl+Space to see all overloads.";
            }

            return doc;
        }

        // Check script state variables
        if (_evaluator?.CurrentState != null)
        {
            ScriptVariable? variable = _evaluator.CurrentState.Variables.FirstOrDefault(v =>
                v.Name == symbolName
            );
            if (variable != null)
            {
                doc.Summary = "Variable defined in the current script scope.";
                doc.Signature = $"{FormatTypeName(variable.Type)} {symbolName}";
                doc.ReturnType = FormatTypeName(variable.Type);

                try
                {
                    object? value = variable.Value;
                    if (value != null)
                    {
                        doc.Remarks = $"Current value: {value}\nType: {value.GetType().FullName}";
                    }
                }
                catch { }

                return doc;
            }
        }

        // Check for static types from imported namespaces
        if (_referencedAssemblies != null)
        {
            foreach (Assembly assembly in _referencedAssemblies)
            {
                try
                {
                    Type? type =
                        assembly.GetType(symbolName)
                        ?? assembly.GetTypes().FirstOrDefault(t => t.Name == symbolName);

                    if (type != null)
                    {
                        doc.Summary =
                            $"{(type.IsClass ? "Class" : type.IsInterface ? "Interface" : type.IsEnum ? "Enum" : "Type")} from {assembly.GetName().Name}.";
                        doc.Signature =
                            $"{(type.IsPublic ? "public" : "internal")} {(type.IsClass ? "class" : type.IsInterface ? "interface" : type.IsEnum ? "enum" : "type")} {type.Name}";
                        doc.Remarks =
                            $"Namespace: {type.Namespace}\nAssembly: {assembly.GetName().Name}";

                        if (type.IsEnum)
                        {
                            IEnumerable<string> enumValues = Enum.GetNames(type).Take(10);
                            doc.Example = $"Values: {string.Join(", ", enumValues)}";
                        }

                        return doc;
                    }
                }
                catch { }
            }
        }

        // Not found - provide generic message
        doc.Summary = "No detailed documentation available for this symbol.";
        doc.Remarks = "This may be a keyword, namespace, or symbol not currently in scope.";

        return doc;
    }

    /// <summary>
    ///     Formats a type name to be more readable (shortens common types).
    /// </summary>
    private string FormatTypeName(Type type)
    {
        if (type == typeof(void))
        {
            return "void";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(float))
        {
            return "float";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        if (type.IsGenericType)
        {
            Type genericType = type.GetGenericTypeDefinition();
            Type[] genericArgs = type.GetGenericArguments();
            IEnumerable<string> genericArgNames = genericArgs.Select(FormatTypeName);

            string baseName = genericType.Name.Split('`')[0];
            return $"{baseName}<{string.Join(", ", genericArgNames)}>";
        }

        return type.Name;
    }
}
