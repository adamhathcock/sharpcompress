using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace SharpCompress.Performance;

internal class Program
{
    static void Main(string[] args)
    {
        // Run all benchmarks in the assembly
        var config = DefaultConfig.Instance;

        // BenchmarkRunner will find all classes with [Benchmark] attributes
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
