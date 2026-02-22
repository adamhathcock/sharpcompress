using System;
using System.IO;

namespace SharpCompress.Cli.Test;

internal static class CliTestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string TestArchivesDirectory { get; } =
        Path.Combine(RepositoryRoot, "tests", "TestArchives", "Archives");

    public static string SnapshotDirectory { get; } =
        Path.Combine(RepositoryRoot, "tests", "SharpCompress.Cli.Test", "Snapshots");

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SharpCompress.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
