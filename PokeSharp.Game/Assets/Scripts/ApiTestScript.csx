using System;
using Microsoft.Xna.Framework;
using PokeSharp.Scripting.Runtime;

/// <summary>
/// Test script for validating Phase 2 direct domain API access.
/// Tests both helper methods and direct domain API calls for ShowMessage and SpawnEffect.
/// </summary>
public class ApiTestScript : TypeScriptBase
{
    public override void OnInitialize(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("ApiTestScript initialized - Testing Phase 2 Direct Domain APIs");

        // Test 1: ShowMessage helper with simple message
        ShowMessage(ctx, "Phase 2 API Test: Dialogue system working!");

        // Test 2: ShowMessage helper with speaker
        ShowMessage(ctx, "Testing dialogue with speaker attribution", speakerName: "Test System");

        // Test 3: ShowMessage helper with priority
        ShowMessage(ctx, "High priority message!", priority: 10);

        // Test 4: Direct domain API call via ScriptContext
        ctx.Dialogue.ShowMessage("Direct domain API call successful!", "DialogueApi");

        // Test 5: SpawnEffect helper
        SpawnEffect(ctx, "test-explosion", new Point(10, 10));

        // Test 6: SpawnEffect with parameters
        SpawnEffect(ctx, "test-heal", new Point(15, 15), duration: 2.0f, scale: 1.5f);

        // Test 7: SpawnEffect with tint
        SpawnEffect(ctx, "test-sparkle", new Point(20, 20), tint: Color.Gold);

        // Test 8: Direct domain API effect call via ScriptContext
        ctx.Effects.SpawnEffect("test-fireball", new Point(25, 25), 1.0f, 2.0f, Color.Red);

        ctx.Logger.LogInformation("All API tests executed successfully!");
    }

    public override void OnActivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("ApiTestScript activated");
        ShowMessage(ctx, "Script activated - APIs ready for testing");
    }

    private float _elapsed = 0f;
    private int _testCycle = 0;

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        _elapsed += deltaTime;

        // Run tests every 5 seconds
        if (_elapsed >= 5.0f)
        {
            _elapsed = 0f;
            _testCycle++;

            // Cycle through different tests
            switch (_testCycle % 4)
            {
                case 0:
                    ShowMessage(ctx, $"Periodic test cycle {_testCycle}: Simple message");
                    break;
                case 1:
                    ShowMessage(ctx, $"Test cycle {_testCycle}: With speaker", "Automated Test");
                    SpawnEffect(ctx, "test-effect-1", new Point(5, 5));
                    break;
                case 2:
                    ctx.Dialogue.ShowMessage($"Direct domain API call cycle {_testCycle}", "DialogueApi Test");
                    break;
                case 3:
                    ctx.Effects.SpawnEffect("test-effect-2", new Point(10, 10), 1.5f, 1.0f);
                    break;
            }
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("ApiTestScript deactivated - completed {TestCycles} test cycles", _testCycle);
        ShowMessage(ctx, "Test script shutting down - all APIs functional!");
    }
}

// Instantiate the script (required by Roslyn)
new ApiTestScript()
