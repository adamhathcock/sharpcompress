using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Xunit;

namespace SharpCompress.Cli.Test;

// Compare the parsed JSON so snapshots do not depend on whitespace or property order.
internal static class JsonSnapshot
{
    internal static void Match(string json, string name, [CallerFilePath] string sourceFile = "")
    {
        var directory = Path.GetDirectoryName(sourceFile)!;
        var filename = $"ArchiveCompatibilityTests.{name}.json";
        var path = Path.Combine(directory, "__snapshots__", filename);
        Assert.True(File.Exists(path), $"Missing snapshot: {filename}");
        var expectedJson = JsonNode.Parse(File.ReadAllText(path));
        var actualJson = JsonNode.Parse(json);
        Assert.True(
            JsonNode.DeepEquals(expectedJson, actualJson),
            $"Snapshot mismatch: {filename}\nExpected: {expectedJson?.ToJsonString()}\nActual: {actualJson?.ToJsonString()}"
        );
    }
}
