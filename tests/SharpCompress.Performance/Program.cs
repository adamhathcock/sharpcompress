using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace SharpCompress.Performance;

public class Program
{
    public static void Main(string[] args)
    {
        // Check if profiling mode is requested
        if (args.Length > 0 && args[0].Equals("--profile", StringComparison.OrdinalIgnoreCase))
        {
            RunWithProfiler(args);
            return;
        }

        // Default: Run BenchmarkDotNet
        var config = DefaultConfig.Instance.AddJob(
            Job.Default.WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(3) // Minimal warmup iterations for CI
                .WithIterationCount(10) // Minimal measurement iterations for CI
                .WithInvocationCount(10)
                .WithUnrollFactor(1)
        );

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }

    private static void RunWithProfiler(string[] args)
    {
        var profileType = "cpu"; // Default to CPU profiling
        var outputPath = "./profiler-snapshots";

        // Parse arguments
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--type", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                profileType = args[++i].ToLowerInvariant();
            }
            else if (
                args[i].Equals("--output", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length
            )
            {
                outputPath = args[++i];
            }
        }

        Console.WriteLine($"Running with JetBrains Profiler ({profileType} mode)");
        Console.WriteLine($"Output path: {outputPath}");
        Console.WriteLine();
        Console.WriteLine(
            "Usage: dotnet run --project SharpCompress.Performance.csproj -c Release -- --profile [--type cpu|memory] [--output <path>]"
        );
        Console.WriteLine();

        // Run a sample benchmark with profiling
        RunSampleBenchmarkWithProfiler(profileType, outputPath);
    }

    private static void RunSampleBenchmarkWithProfiler(string profileType, string outputPath)
    {
        Console.WriteLine("Running sample benchmark with profiler...");
        Console.WriteLine("Note: JetBrains profiler requires the profiler tools to be installed.");
        Console.WriteLine("Install from: https://www.jetbrains.com/profiler/");
        Console.WriteLine();

        try
        {
            IDisposable? profiler = null;

            if (profileType == "cpu")
            {
                profiler = Test.JetbrainsProfiler.Cpu(outputPath);
            }
            else if (profileType == "memory")
            {
                profiler = Test.JetbrainsProfiler.Memory(outputPath);
            }

            using (profiler)
            {
                // Run a simple benchmark iteration
                var zipBenchmark = new Benchmarks.ZipBenchmarks();
                zipBenchmark.Setup();

                Console.WriteLine("Running benchmark iterations...");
                for (int i = 0; i < 10; i++)
                {
                    zipBenchmark.ZipExtractArchiveApi();
                    if (i % 3 == 0)
                    {
                        Console.Write(".");
                    }
                }
                Console.WriteLine();
                Console.WriteLine("Benchmark iterations completed.");
            }

            Console.WriteLine($"Profiler snapshot saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running profiler: {ex.Message}");
            Console.WriteLine("Make sure JetBrains profiler tools are installed and accessible.");
        }
    }
}
