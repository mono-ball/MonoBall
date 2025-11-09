using BenchmarkDotNet.Running;
using PokeSharp.Benchmarks;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Main entry point for benchmark execution.
///     Supports running all benchmarks or specific benchmark suites.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            // No arguments - show menu
            ShowMenu();
            return;
        }

        // Parse command line arguments
        var command = args[0].ToLower();

        switch (command)
        {
            case "all":
                RunAllBenchmarks(args.Skip(1).ToArray());
                break;

            case "entity":
                BenchmarkRunner.Run<EntityCreationBenchmarks>(GetConfig(args));
                break;

            case "query":
                BenchmarkRunner.Run<QueryBenchmarks>(GetConfig(args));
                break;

            case "system":
                BenchmarkRunner.Run<SystemBenchmarks>(GetConfig(args));
                break;

            case "memory":
                BenchmarkRunner.Run<MemoryAllocationBenchmarks>(GetConfig(args));
                break;

            case "spatial":
                BenchmarkRunner.Run<SpatialHashBenchmarks>(GetConfig(args));
                break;

            case "help":
            case "-h":
            case "--help":
                ShowHelp();
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Run with 'help' for usage information.");
                break;
        }
    }

    /// <summary>
    ///     Run all benchmark suites.
    /// </summary>
    private static void RunAllBenchmarks(string[] args)
    {
        Console.WriteLine("Running all benchmark suites...");
        Console.WriteLine();

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    /// <summary>
    ///     Show interactive menu for benchmark selection.
    /// </summary>
    private static void ShowMenu()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           PokeSharp ECS Performance Benchmarks                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Available benchmark suites:");
        Console.WriteLine();
        Console.WriteLine("  1. Entity Creation    - Entity creation and component attachment");
        Console.WriteLine("  2. Query Performance  - ECS query execution speed");
        Console.WriteLine("  3. System Updates     - System update and game loop");
        Console.WriteLine("  4. Memory Allocation  - Memory allocation patterns");
        Console.WriteLine("  5. Spatial Hash       - Spatial hash system performance");
        Console.WriteLine("  6. Run All           - Execute all benchmark suites");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -c Release -- <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  entity   - Run entity creation benchmarks");
        Console.WriteLine("  query    - Run query performance benchmarks");
        Console.WriteLine("  system   - Run system update benchmarks");
        Console.WriteLine("  memory   - Run memory allocation benchmarks");
        Console.WriteLine("  spatial  - Run spatial hash benchmarks");
        Console.WriteLine("  all      - Run all benchmarks");
        Console.WriteLine("  help     - Show detailed help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -c Release -- entity");
        Console.WriteLine("  dotnet run -c Release -- all --filter *Query*");
        Console.WriteLine("  dotnet run -c Release -- system --runtimes net9.0");
        Console.WriteLine();
    }

    /// <summary>
    ///     Show detailed help information.
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("PokeSharp ECS Benchmarks - Detailed Help");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("COMMANDS:");
        Console.WriteLine("  entity   - Entity creation and component attachment benchmarks");
        Console.WriteLine("  query    - ECS query performance at various scales");
        Console.WriteLine("  system   - System update and game loop benchmarks");
        Console.WriteLine("  memory   - Memory allocation and GC pressure benchmarks");
        Console.WriteLine("  spatial  - Spatial hash system performance benchmarks");
        Console.WriteLine("  all      - Run all benchmark suites");
        Console.WriteLine();
        Console.WriteLine("COMMON OPTIONS:");
        Console.WriteLine("  --filter <pattern>    - Filter benchmarks by name pattern");
        Console.WriteLine("  --job <name>          - Specify job configuration");
        Console.WriteLine("  --runtimes <names>    - Specify runtime(s) to test");
        Console.WriteLine("  --exporters <names>   - Specify result exporters");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine();
        Console.WriteLine("  # Run entity creation benchmarks");
        Console.WriteLine("  dotnet run -c Release -- entity");
        Console.WriteLine();
        Console.WriteLine("  # Run all benchmarks with JSON export");
        Console.WriteLine("  dotnet run -c Release -- all --exporters json");
        Console.WriteLine();
        Console.WriteLine("  # Run query benchmarks with 1000 entities only");
        Console.WriteLine("  dotnet run -c Release -- query --filter *1000*");
        Console.WriteLine();
        Console.WriteLine("  # Run memory benchmarks for detailed allocation tracking");
        Console.WriteLine("  dotnet run -c Release -- memory");
        Console.WriteLine();
        Console.WriteLine("For more information, visit:");
        Console.WriteLine("  https://benchmarkdotnet.org/articles/overview.html");
        Console.WriteLine();
    }

    /// <summary>
    ///     Extract configuration from command line arguments.
    /// </summary>
    private static BenchmarkDotNet.Configs.IConfig? GetConfig(string[] args)
    {
        // BenchmarkRunner will handle config from args automatically
        return null;
    }
}
