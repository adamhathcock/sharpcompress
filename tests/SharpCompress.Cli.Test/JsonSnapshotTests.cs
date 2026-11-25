using System;
using System.IO;
using Xunit;

namespace SharpCompress.Cli.Test;

public class JsonSnapshotTests
{
    [Fact]
    public void MatchComparesJsonStructureRatherThanFormatting()
    {
        var directory = CreateDirectory();
        try
        {
            WriteSnapshot(directory, "{ \"items\": [1, 2], \"name\": \"test\" }");
            JsonSnapshot.Match(
                "{\"name\":\"test\",\"items\":[1,2]}",
                "comparison",
                Path.Combine(directory, "test.cs")
            );
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Theory]
    [InlineData("{\"items\":[2,1],\"name\":\"test\"}")]
    [InlineData("{\"items\":[1,2],\"name\":\"changed\"}")]
    [InlineData("{\"items\":[1,2]}")]
    public void MatchRejectsChangedJson(string actual)
    {
        var directory = CreateDirectory();
        try
        {
            WriteSnapshot(directory, "{\"items\":[1,2],\"name\":\"test\"}");
            var error = Assert.ThrowsAny<Exception>(() =>
                JsonSnapshot.Match(actual, "comparison", Path.Combine(directory, "test.cs"))
            );
            Assert.Contains("Snapshot mismatch", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void MatchFailsWhenBaselineIsMissing()
    {
        var directory = CreateDirectory();
        try
        {
            var error = Assert.ThrowsAny<Exception>(() =>
                JsonSnapshot.Match("{}", "comparison", Path.Combine(directory, "test.cs"))
            );
            Assert.Contains("Missing snapshot", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "__snapshots__"));
        return directory;
    }

    private static void WriteSnapshot(string directory, string contents) =>
        File.WriteAllText(
            Path.Combine(directory, "__snapshots__", "ArchiveCompatibilityTests.comparison.json"),
            contents
        );

    private static void DeleteDirectory(string directory)
    {
        var snapshotDirectory = Path.Combine(directory, "__snapshots__");
        File.Delete(Path.Combine(snapshotDirectory, "ArchiveCompatibilityTests.comparison.json"));
        Directory.Delete(snapshotDirectory);
        Directory.Delete(directory);
    }
}
