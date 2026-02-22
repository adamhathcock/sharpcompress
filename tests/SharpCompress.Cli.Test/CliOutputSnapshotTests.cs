using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace SharpCompress.Cli.Test;

public class CliOutputSnapshotTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = true,
    };

    public static IEnumerable<object[]> InspectArchiveCases()
    {
        yield return ["Zip.deflate.zip", "inspect-zip"];
        yield return ["Tar.tar", "inspect-tar"];
        yield return ["Tar.tar.gz", "inspect-gzip"];
        yield return ["Rar.rar", "inspect-rar"];
        yield return ["7Zip.LZMA.7z", "inspect-7zip"];
        yield return ["Arc.uncompressed.arc", "inspect-arc"];
        yield return ["Arj.store.arj", "inspect-arj"];
        yield return ["Ace.store.ace", "inspect-ace"];
        yield return ["large_test.txt.Z", "inspect-lzw"];
    }

    [Theory]
    [MemberData(nameof(InspectArchiveCases))]
    public Task Inspect_Command_JsonOutput_MatchesSnapshot(
        string archiveFileName,
        string snapshotName
    )
    {
        var archivePath = Path.Combine(CliTestPaths.TestArchivesDirectory, archiveFileName);
        Assert.True(File.Exists(archivePath), $"Archive file not found: {archivePath}");

        var result = CliCommandTestHost.Invoke(
            "inspect",
            archivePath,
            "--format",
            "json",
            "--limit",
            "2"
        );
        Assert.True(
            string.IsNullOrWhiteSpace(result.StdErr),
            $"CLI wrote to stderr:{Environment.NewLine}{result.StdErr}"
        );

        Assert.Equal(GetExpectedExitCode(result.StdOut), result.ExitCode);

        var normalized = NormalizeInspectOutput(result.StdOut);

        var settings = new VerifySettings();
        settings.UseDirectory(Path.Combine(CliTestPaths.SnapshotDirectory, "Inspect"));
        settings.UseFileName(snapshotName);

        return Verifier
            .Verify(target: normalized, settings: settings, extension: "json")
            .UseStrictJson();
    }

    private static int GetExpectedExitCode(string rawOutput)
    {
        var root = JsonNode.Parse(rawOutput) as JsonObject;
        Assert.NotNull(root);

        var errors = root["errors"] as JsonArray;
        return errors is { Count: > 0 } ? 1 : 0;
    }

    private static string NormalizeInspectOutput(string rawOutput)
    {
        var root = JsonNode.Parse(rawOutput) as JsonObject;
        Assert.NotNull(root);

        var archives = root["archives"] as JsonArray;
        if (archives is not null)
        {
            foreach (var archiveNode in archives)
            {
                if (archiveNode is JsonObject archiveObject)
                {
                    archiveObject["archivePath"] = "<ARCHIVE_PATH>";
                }
            }
        }

        var errors = root["errors"] as JsonArray;
        if (errors is not null)
        {
            foreach (var errorNode in errors)
            {
                if (errorNode is JsonObject errorObject)
                {
                    errorObject["archivePath"] = "<ARCHIVE_PATH>";
                }
            }
        }

        return JsonSerializer.Serialize(root, SnapshotJsonOptions);
    }
}
