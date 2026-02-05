using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace SharpCompress.Performance;

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance.AddJob(
            Job.Default.WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(1) // Minimal warmup iterations for CI
                .WithIterationCount(3) // Minimal measurement iterations for CI
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
        );

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
