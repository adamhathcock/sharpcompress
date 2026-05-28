using System;
using System.IO;

namespace SharpCompress.Performance.Benchmarks;

public abstract class ArchiveBenchmarkBase
{
    protected static readonly string TEST_ARCHIVES_PATH;

    static ArchiveBenchmarkBase()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var index = baseDirectory.IndexOf(
            "SharpCompress.Performance",
            StringComparison.OrdinalIgnoreCase
        );

        if (index == -1)
        {
            throw new InvalidOperationException(
                "Could not find SharpCompress.Performance in the base directory path"
            );
        }

        var path = baseDirectory.Substring(0, index);
        var solutionBasePath = Path.GetDirectoryName(path) ?? throw new InvalidOperationException();
        TEST_ARCHIVES_PATH = Path.Combine(solutionBasePath, "TestArchives", "Archives");

        if (!Directory.Exists(TEST_ARCHIVES_PATH))
        {
            throw new InvalidOperationException(
                $"Test archives directory not found: {TEST_ARCHIVES_PATH}"
            );
        }
    }

    protected static string GetArchivePath(string fileName) =>
        Path.Combine(TEST_ARCHIVES_PATH, fileName);
}
