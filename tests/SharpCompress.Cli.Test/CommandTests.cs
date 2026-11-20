using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Cli;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Providers;
using Xunit;

namespace SharpCompress.Cli.Test;

public class CommandTests
{
    [Theory]
    [InlineData("detect", "--help", "Identify an input file")]
    [InlineData("list", "--help", "--long")]
    [InlineData("formats", "--help", "Optional format name")]
    public async Task CommandHelpDescribesItsArguments(string command, string help, string expected)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            0,
            await CliApplication.RunAsync([command, help], output, error, CancellationToken.None)
        );
        Assert.Contains(expected, output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [InlineData("detect")]
    [InlineData("inspect")]
    [InlineData("list")]
    public async Task MissingInputIsStructuredParseError(string command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            2,
            await CliApplication.RunAsync(
                [command, "--json"],
                output,
                error,
                CancellationToken.None
            )
        );
        using var document = JsonDocument.Parse(error.ToString());
        Assert.Equal(2, document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public async Task OptionValuesAndEndOfOptionsAreParsedCorrectly()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "-test.zip");
        try
        {
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                zip.CreateEntry("test.txt");
            }
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(
                0,
                await CliApplication.RunAsync(
                    ["list", "--encoding=utf-8", "--json", "--", path],
                    output,
                    error,
                    CancellationToken.None
                )
            );
            using var document = JsonDocument.Parse(output.ToString());
            Assert.Single(
                document.RootElement.GetProperty("result").GetProperty("entries").EnumerateArray()
            );
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task IncompatibleOutputModesAreRejected()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            2,
            await CliApplication.RunAsync(
                ["formats", "--json", "--ndjson"],
                output,
                error,
                CancellationToken.None
            )
        );
        Assert.Equal("", output.ToString());
        using var document = JsonDocument.Parse(error.ToString());
        Assert.Equal(2, document.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Theory]
    [InlineData(CompressionType.BZip2)]
    [InlineData(CompressionType.LZip)]
    [InlineData(CompressionType.ZStandard)]
    public async Task StandaloneStreamsAreNotMisclassifiedAsArchives(CompressionType compression)
    {
        var path = Path.GetTempFileName();
        try
        {
            await using (var file = File.Create(path))
            await using (
                var encoder = await CompressionProviderRegistry.Default.CreateCompressStreamAsync(
                    compression,
                    file,
                    3,
                    CancellationToken.None
                )
            )
            {
                await encoder.WriteAsync(
                    "standalone payload\n"u8.ToArray(),
                    CancellationToken.None
                );
            }
            var detection = await FileDetector.DetectFileAsync(
                path,
                cancellationToken: CancellationToken.None
            );
            Assert.NotNull(detection);
            Assert.True(detection.IsStandaloneCompression);
            Assert.Equal(compression, detection.Compression);
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(
                0,
                await CliApplication.RunAsync(
                    ["list", path, "--json"],
                    output,
                    error,
                    CancellationToken.None
                )
            );
            Assert.Contains("logicalPayload", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StandaloneXzIsRecognizedWithoutAnArchiveApi()
    {
        // XZ stream for "standalone payload\n", generated by Python's liblzma binding.
        using var stream = new MemoryStream(
            Convert.FromBase64String(
                "/Td6WFoAAATm1rRGAgAhARYAAAB0L+WjAQASc3RhbmRhbG9uZSBwYXlsb2FkCgAAJM0E4nRNUqgAASsTnAlI0h+2830BAAAAAARZWg=="
            )
        );
        var detection = await FileDetector.DetectFileAsync(
            stream,
            cancellationToken: CancellationToken.None
        );
        Assert.NotNull(detection);
        Assert.True(detection.IsStandaloneCompression);
        Assert.Equal(CompressionType.Xz, detection.Compression);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Zip.none.datadescriptors.zip")]
    [InlineData("Tar.tar")]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.bz2")]
    [InlineData("Tar.tar.xz")]
    [InlineData("Tar.tar.zst")]
    [InlineData("Tar.tar.lz")]
    [InlineData("large_test.txt.Z")]
    [InlineData("Rar.solid.rar")]
    [InlineData("Rar.multi.solid.part01.rar")]
    [InlineData("7Zip.solid.7z")]
    [InlineData("Ace.store.ace")]
    [InlineData("Arc.uncompressed.arc")]
    [InlineData("Arj.store.arj")]
    public async Task ExistingFormatsSupportReadOnlyCommands(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SharpCompress.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "tests", "TestArchives", "Archives", name);
        foreach (var command in new[] { "detect", "inspect", "list" })
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exit = await CliApplication.RunAsync(
                [command, path, "--json"],
                output,
                error,
                CancellationToken.None
            );
            Assert.True(exit == 0, command + ": " + error);
            using var document = JsonDocument.Parse(output.ToString());
        }
    }

    [Theory]
    [InlineData("detect")]
    [InlineData("inspect")]
    [InlineData("list")]
    public async Task CommandsHandleDuplicateZipNames(string command)
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                zip.CreateEntry("same.txt");
                zip.CreateEntry("same.txt");
            }
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(
                0,
                await CliApplication.RunAsync(
                    [command, path, "--json"],
                    output,
                    error,
                    CancellationToken.None
                )
            );
            using var document = JsonDocument.Parse(output.ToString());
            Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("", error.ToString());
            if (command == "list")
            {
                Assert.Equal(
                    2,
                    document
                        .RootElement.GetProperty("result")
                        .GetProperty("entries")
                        .GetArrayLength()
                );
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UnknownFileAndInvalidArgumentsHaveDistinctExitCodes()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "not an archive");
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(
                1,
                await CliApplication.RunAsync(
                    ["detect", path, "--json"],
                    output,
                    error,
                    CancellationToken.None
                )
            );
            using var document = JsonDocument.Parse(output.ToString());
            var result = document.RootElement.GetProperty("result");
            Assert.Equal("unrecognized", result.GetProperty("status").GetString());
            Assert.False(result.TryGetProperty("detection", out _));
            Assert.Equal(
                2,
                await CliApplication.RunAsync(
                    ["detect", path, "--bad"],
                    output,
                    error,
                    CancellationToken.None
                )
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FormatsHasArchiveAndCompressionRecords()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            0,
            await CliApplication.RunAsync(
                ["formats", "--ndjson"],
                output,
                error,
                CancellationToken.None
            )
        );
        Assert.Contains("compressionStream", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("archive", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectorPreservesStreamPositionAndOwnership()
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[17]);
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write("some text"u8);
        }
        stream.Position = 17;
        Assert.NotNull(
            await FileDetector.DetectFileAsync(stream, cancellationToken: CancellationToken.None)
        );
        Assert.Equal(17, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task CancellationHasDedicatedExitCode()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(
                130,
                await CliApplication.RunAsync(
                    ["detect", path],
                    output,
                    error,
                    new CancellationToken(true)
                )
            );
        }
        finally
        {
            File.Delete(path);
        }
    }
}
