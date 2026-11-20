using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Cli;
using Xunit;

namespace SharpCompress.Cli.Test;

public class ArchiveCompatibilityTests
{
    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Zip.none.datadescriptors.zip")]
    [InlineData("Tar.tar")]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.bz2")]
    [InlineData("Tar.tar.xz")]
    [InlineData("Tar.tar.zst")]
    [InlineData("Tar.tar.lz")]
    [InlineData("Rar.solid.rar")]
    [InlineData("Rar.multi.solid.part01.rar")]
    [InlineData("7Zip.solid.7z")]
    [InlineData("Ace.store.ace")]
    [InlineData("Arc.uncompressed.arc")]
    [InlineData("Arj.store.arj")]
    [InlineData("large_test.txt.Z")]
    public async Task FixtureDetectionInspectionAndListingAgree(string filename)
    {
        var path = ArchivePath(filename);
        using var detected = await RunJsonAsync("detect", path);
        using var inspected = await RunJsonAsync("inspect", path);
        using var listed = await RunJsonAsync("list", path);
        var snapshot = new JsonObject
        {
            ["detect"] = Normalize(detected, filename),
            ["inspect"] = Normalize(inspected, filename),
            ["list"] = Normalize(listed, filename),
        };
        JsonSnapshot.Match(
            snapshot.ToJsonString(),
            $"FixtureDetectionInspectionAndListingAgree_filename={filename}"
        );
    }

    [Fact]
    public async Task TarLocalPaxMetadataAppliesOnlyToTheNextEntry()
    {
        using var document = await RunJsonAsync("list", ArchivePath("Tar.PaxLocalHeader.tar"));
        var entries = document.RootElement.GetProperty("result").GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());
        AssertTarEntry(entries[0], 0, "pax/overridden-name.txt", "0640", 1234, 2345);
        Assert.Equal(10, entries[0].GetProperty("metadata").GetProperty("size").GetInt64());
        AssertTarEntry(entries[1], 1, "second.txt", "0644", 11, 22);
        Assert.Equal(2, entries[1].GetProperty("metadata").GetProperty("size").GetInt64());
    }

    [Fact]
    public async Task TarGlobalPaxMetadataResumesAfterLocalOverride()
    {
        using var document = await RunJsonAsync("list", ArchivePath("Tar.PaxGlobalHeader.tar"));
        var entries = document.RootElement.GetProperty("result").GetProperty("entries");
        Assert.Equal(3, entries.GetArrayLength());
        AssertTarEntry(entries[0], 0, "global-one.txt", "0640", 4000, 5000);
        AssertTarEntry(entries[1], 1, "global-local-override.txt", "0600", 4010, 5010);
        AssertTarEntry(entries[2], 2, "global-three.txt", "0640", 4000, 5000);
    }

    [Fact]
    public async Task TarDetailAppearsInNdjsonAndLongListingsButNotPlainListings()
    {
        var path = ArchivePath("Tar.PaxLocalHeader.tar");
        using var document = await RunJsonAsync("list", path);
        var entries = document.RootElement.GetProperty("result").GetProperty("entries");
        Assert.False(entries[0].GetProperty("metadata").TryGetProperty("linkTarget", out _));

        var ndjson = await RunProcessAsync(["list", path, "--ndjson"]);
        Assert.Equal(0, ndjson.ExitCode);
        Assert.Equal("", ndjson.Error);
        var records = ndjson.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(entries.GetArrayLength(), records.Length);
        for (var i = 0; i < records.Length; i++)
        {
            using var record = JsonDocument.Parse(records[i]);
            Assert.Equal(1, record.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.True(
                JsonElement.DeepEquals(entries[i], record.RootElement.GetProperty("result"))
            );
        }

        var detailed = await RunProcessAsync(["list", path, "--long"]);
        Assert.Equal(0, detailed.ExitCode);
        Assert.Equal("", detailed.Error);
        Assert.Contains("tar.mode: \"0640\"", detailed.Output, StringComparison.Ordinal);
        Assert.Contains("tar.userId: 1234", detailed.Output, StringComparison.Ordinal);
        Assert.Contains("tar.groupId: 2345", detailed.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata.linkTarget:", detailed.Output, StringComparison.Ordinal);

        var plain = await RunProcessAsync(["list", path]);
        Assert.Equal(0, plain.ExitCode);
        Assert.Equal("", plain.Error);
        Assert.Equal(
            new[] { "pax/overridden-name.txt", "second.txt" },
            plain
                .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
        );
    }

    [Fact]
    public async Task TarDirectoryOmitsModeButKeepsZeroOwnershipAndFalseFlags()
    {
        using var document = await RunJsonAsync("list", ArchivePath("Tar.tar"));
        var entry = document.RootElement.GetProperty("result").GetProperty("entries")[0];
        var metadata = entry.GetProperty("metadata");
        Assert.True(metadata.GetProperty("isDirectory").GetBoolean());
        Assert.False(metadata.GetProperty("isEncrypted").GetBoolean());
        Assert.Equal(0, metadata.GetProperty("size").GetInt64());
        var tar = entry.GetProperty("tar");
        Assert.False(tar.TryGetProperty("mode", out _));
        Assert.Equal(0, tar.GetProperty("userId").GetInt64());
        Assert.Equal(0, tar.GetProperty("groupId").GetInt64());
    }

    [Fact]
    public async Task ReaderOnlyEntriesOmitUnavailableSizes()
    {
        using var document = await RunJsonAsync("list", ArchivePath("Arc.uncompressed.arc"));
        var first = document
            .RootElement.GetProperty("result")
            .GetProperty("entries")[0]
            .GetProperty("metadata");
        Assert.Equal("TECT.TXT", first.GetProperty("key").GetString());
        Assert.False(first.TryGetProperty("size", out _));
        Assert.Equal(15498, first.GetProperty("compressedSize").GetInt64());
        Assert.False(first.TryGetProperty("isComplete", out _));
    }

    [Theory]
    [InlineData("Rar.solid.rar")]
    [InlineData("7Zip.solid.7z")]
    public async Task SolidArchivesExposeSharedStreamMetadata(string filename)
    {
        using var document = await RunJsonAsync("inspect", ArchivePath(filename));
        var info = document.RootElement.GetProperty("result").GetProperty("information");
        Assert.True(info.GetProperty("isSolid").GetBoolean());
        Assert.Equal(1, info.GetProperty("solidStreamCount").GetInt32());
    }

    [Fact]
    public async Task ZipDataDescriptorsAreIdentifiedInInspection()
    {
        using var document = await RunJsonAsync(
            "inspect",
            ArchivePath("Zip.none.datadescriptors.zip")
        );
        var info = document.RootElement.GetProperty("result").GetProperty("information");
        Assert.Equal(2, info.GetProperty("entriesWithUnknownSizeCount").GetInt32());
        Assert.True(
            info.GetProperty("zip").GetProperty("hasEntriesWithDeferredSizes").GetBoolean()
        );
    }

    [Fact]
    public async Task SplitArchivePartsAreDiscovered()
    {
        using var document = await RunJsonAsync(
            "inspect",
            ArchivePath("Rar.multi.solid.part01.rar")
        );
        var info = document.RootElement.GetProperty("result").GetProperty("information");
        Assert.True(info.GetProperty("isMultiVolume").GetBoolean());
        Assert.True(info.GetProperty("isComplete").GetBoolean());
        Assert.Equal(6, info.GetProperty("physicalPartCount").GetInt32());
    }

    [Fact]
    public async Task MissingSplitArchivePartsAreReportedAsPartial()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Rar.multi.solid.part01.rar");
        try
        {
            File.Copy(ArchivePath("Rar.multi.solid.part01.rar"), path);
            using var document = await RunJsonAsync("inspect", path);
            var info = document.RootElement.GetProperty("result").GetProperty("information");
            Assert.Equal("Partial", info.GetProperty("status").GetString());
            Assert.Equal("MissingVolumes", info.GetProperty("limitations").GetString());
            Assert.False(info.GetProperty("isComplete").GetBoolean());
            Assert.Equal(1, info.GetProperty("physicalPartCount").GetInt32());
            Assert.False(info.TryGetProperty("compressedPayloadSize", out _));
            JsonSnapshot.Match(
                Normalize(document, Path.GetFileName(path)).ToJsonString(),
                nameof(MissingSplitArchivePartsAreReportedAsPartial)
            );
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task DetectionOfCorruptXzDoesNotClaimIntegrity()
    {
        var path = ArchivePath("bad-1-lzma2-7.xz");
        using var detection = await RunJsonAsync("detect", path);
        var format = detection.RootElement.GetProperty("result").GetProperty("detection");
        Assert.True(format.GetProperty("isStandaloneCompression").GetBoolean());
        Assert.Equal("Xz", format.GetProperty("compression").GetString());
        using var inspection = await RunJsonAsync("inspect", path);
        Assert.Equal(
            "notTested",
            inspection.RootElement.GetProperty("result").GetProperty("integrity").GetString()
        );
    }

    [Fact]
    public async Task EncryptedHeadersProducePartialInspection()
    {
        using var document = await RunJsonAsync(
            "inspect",
            ArchivePath("Rar.encrypted_filesAndHeader.rar")
        );
        var info = document.RootElement.GetProperty("result").GetProperty("information");
        Assert.Equal("Partial", info.GetProperty("status").GetString());
        Assert.Equal("EncryptedHeaders", info.GetProperty("limitations").GetString());
        Assert.False(info.TryGetProperty("entryCount", out _));
        JsonSnapshot.Match(
            Normalize(document, "Rar.encrypted_filesAndHeader.rar").ToJsonString(),
            nameof(EncryptedHeadersProducePartialInspection)
        );
    }

    [Fact]
    public async Task CommandLineProcessHandlesPasswordAndNdjson()
    {
        var path = ArchivePath("Rar.encrypted_filesAndHeader.rar");
        var result = await RunProcessAsync(
            ["list", path, "--password-env", "SHARPCOMPRESS_CLI_TEST_PASSWORD", "--ndjson"],
            "test"
        );
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Error);
        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        for (var i = 0; i < lines.Length; i++)
        {
            using var record = JsonDocument.Parse(lines[i]);
            Assert.Equal(1, record.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(
                i,
                record.RootElement.GetProperty("result").GetProperty("index").GetInt32()
            );
        }
        Assert.DoesNotContain(
            "SHARPCOMPRESS_CLI_TEST_PASSWORD",
            result.Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task CommandLineProcessReportsWrongPasswordWithoutLeakingIt()
    {
        const string password = "invalid-cli-test-secret";
        var result = await RunProcessAsync(
            [
                "list",
                ArchivePath("Rar.encrypted_filesAndHeader.rar"),
                "--password-env",
                "SHARPCOMPRESS_CLI_TEST_PASSWORD",
                "--json",
            ],
            password
        );
        Assert.Equal(4, result.ExitCode);
        Assert.Equal("", result.Output);
        using var error = JsonDocument.Parse(result.Error);
        Assert.Equal(4, error.RootElement.GetProperty("exitCode").GetInt32());
        Assert.DoesNotContain(password, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlainTextAndNdjsonListTheSameArchiveEntries()
    {
        var path = ArchivePath("Zip.deflate.zip");
        var plain = await RunProcessAsync(["list", path]);
        var structured = await RunProcessAsync(["list", path, "--ndjson"]);
        Assert.Equal(0, plain.ExitCode);
        Assert.Equal(0, structured.ExitCode);
        Assert.Equal("", plain.Error);
        Assert.Equal("", structured.Error);
        var names = plain
            .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
        var records = structured.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(names.Length, records.Length);
        for (var i = 0; i < records.Length; i++)
        {
            using var record = JsonDocument.Parse(records[i]);
            Assert.Equal(
                names[i],
                record
                    .RootElement.GetProperty("result")
                    .GetProperty("metadata")
                    .GetProperty("key")
                    .GetString()
            );
        }
    }

    private static void AssertTarEntry(
        JsonElement entry,
        int index,
        string key,
        string mode,
        long userId,
        long groupId
    )
    {
        Assert.Equal(index, entry.GetProperty("index").GetInt64());
        Assert.Equal(key, entry.GetProperty("metadata").GetProperty("key").GetString());
        var tar = entry.GetProperty("tar");
        Assert.Equal(mode, tar.GetProperty("mode").GetString());
        Assert.Equal(userId, tar.GetProperty("userId").GetInt64());
        Assert.Equal(groupId, tar.GetProperty("groupId").GetInt64());
    }

    private static async Task<JsonDocument> RunJsonAsync(string command, string path)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await CliApplication.RunAsync(
            [command, path, "--json"],
            output,
            error,
            CancellationToken.None
        );
        Assert.True(exit == 0, $"{command}: {error}");
        Assert.Equal("", error.ToString());
        return JsonDocument.Parse(output.ToString());
    }

    private static JsonNode Normalize(JsonDocument document, string filename)
    {
        // Some archive timestamps are converted to local time, so their complete value
        // varies with the machine's timezone. Keep presence and type, not local time.
        var json = Regex.Replace(
            document.RootElement.GetRawText(),
            @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[+-]\d{2}:\d{2}|Z)?(?="")",
            "<timestamp>"
        );
        var node = JsonNode.Parse(json)!;
        if (node["result"] is JsonObject result && result.ContainsKey("path"))
        {
            result["path"] = filename;
        }
        return node;
    }

    private static string ArchivePath(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SharpCompress.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "tests", "TestArchives", "Archives", name);
        Assert.True(File.Exists(path), $"Fixture missing: {path}");
        return path;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string[] arguments,
        string? password = null
    )
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        if (password is not null)
        {
            start.Environment["SHARPCOMPRESS_CLI_TEST_PASSWORD"] = password;
        }
        using var process =
            Process.Start(start)
            ?? throw new InvalidOperationException("Could not start CLI process.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, await output, await error);
    }
}
