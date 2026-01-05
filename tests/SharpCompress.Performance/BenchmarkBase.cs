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

        if (index == -1)
        {
            throw new InvalidOperationException(
                "Could not locate SharpCompress.Performance in the base directory path"
            );
        }

        var path = AppDomain.CurrentDomain.BaseDirectory.Substring(0, index);
        var solutionBasePath =
            Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Could not determine solution base path");

        TEST_ARCHIVES_PATH = Path.Combine(solutionBasePath, "TestArchives", "Archives");
    }

    protected string GetTestArchivePath(string filename) =>
        Path.Combine(TEST_ARCHIVES_PATH, filename);
}
