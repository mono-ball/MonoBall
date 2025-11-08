using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Scripting.Services;
using PokeSharp.Scripting.Runtime;

namespace PokeSharp.Game.Diagnostics;

/// <summary>
/// Initializes and runs the API test script for Phase 1 validation.
/// </summary>
public class ApiTestInitializer
{
    private readonly World _world;
    private readonly ScriptService _scriptService;
    private readonly ILogger<ApiTestInitializer> _logger;

    public ApiTestInitializer(
        World world,
        ScriptService scriptService,
        ILogger<ApiTestInitializer> logger)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Load and initialize the API test script.
    /// </summary>
    public async Task RunApiTestAsync()
    {
        _logger.LogInformation("🧪 Starting Phase 1 API Test...");

        try
        {
            // Load the test script
            var scriptInstance = await _scriptService.LoadScriptAsync("ApiTestScript.csx");

            if (scriptInstance == null)
            {
                _logger.LogError("❌ Failed to load ApiTestScript.csx - script returned null");
                return;
            }

            if (scriptInstance is not TypeScriptBase scriptBase)
            {
                _logger.LogError("❌ ApiTestScript is not a TypeScriptBase - got {Type}", scriptInstance.GetType().Name);
                return;
            }

            _logger.LogInformation("✅ ApiTestScript loaded successfully");

            // Initialize the script (this triggers OnInitialize)
            _scriptService.InitializeScript(scriptInstance, _world, entity: null, _logger);

            _logger.LogInformation("✅ ApiTestScript initialized - check logs for event outputs");
            _logger.LogInformation("📊 Phase 1 API Test Summary:");
            _logger.LogInformation("   - ShowMessage() helper tests: 3");
            _logger.LogInformation("   - WorldApi.ShowMessage() direct tests: 1");
            _logger.LogInformation("   - SpawnEffect() helper tests: 3");
            _logger.LogInformation("   - WorldApi.SpawnEffect() direct tests: 1");
            _logger.LogInformation("   - Total expected events: 8 (4 dialogue + 4 effects)");
            _logger.LogInformation("🔍 Check ApiTestEventSubscriber logs to verify event delivery");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ API Test failed with exception: {Message}", ex.Message);
        }
    }
}
