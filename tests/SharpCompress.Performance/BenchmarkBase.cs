using System;
using System.IO;

namespace SharpCompress.Performance;

/// <summary>
/// Base class for all benchmarks providing common setup for test archives path
/// </summary>
public class BenchmarkBase
{
    protected readonly string TEST_ARCHIVES_PATH;

    public BenchmarkBase()
    {
        var index = AppDomain.CurrentDomain.BaseDirectory.IndexOf(
            "SharpCompress.Performance",
            StringComparison.OrdinalIgnoreCase
        );
        var path = AppDomain.CurrentDomain.BaseDirectory.Substring(0, index);
        var SOLUTION_BASE_PATH = Path.GetDirectoryName(path) ?? throw new ArgumentNullException();

        TEST_ARCHIVES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Archives");
    }

    protected string GetTestArchivePath(string filename) =>
        Path.Combine(TEST_ARCHIVES_PATH, filename);
}
