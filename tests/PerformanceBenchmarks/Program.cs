using PerformanceBenchmarks;

namespace MonoBallFramework.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        // Run comprehensive event performance validation
        EventPerformanceValidator.RunValidation();
    }
}
