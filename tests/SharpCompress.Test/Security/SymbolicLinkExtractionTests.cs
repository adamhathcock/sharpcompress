#if NET8_0_OR_GREATER
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Security;

public class SymbolicLinkExtractionTests : TestBase
{
    private const int TarBlockSize = 512;

    [Theory]
    [InlineData("ReaderAll")]
    [InlineData("ReaderEntry")]
    [InlineData("Archive")]
    [InlineData("ArchiveEntry")]
    [InlineData("AsyncReaderAll")]
    [InlineData("AsyncReaderEntry")]
    [InlineData("AsyncArchive")]
    [InlineData("AsyncArchiveEntry")]
    public async Task SymbolicLinkTargetOutsideDestination_ShouldThrowBeforeInvokingHandler(
        string api
    )
    {
        var destinationDirectory = GetScratchPath("extract");
        var outsideDirectory = GetScratchPath("outside");
        var archivePath = GetScratch2Path("symbolic-link-outside.tar");
        Directory.CreateDirectory(destinationDirectory);
        Directory.CreateDirectory(outsideDirectory);
        BuildTar(archivePath, "../outside");

        var handlerCalls = 0;
        var options = new ExtractionOptions { SymbolicLinkHandler = (_, _) => handlerCalls++ };

        var exception = await ExtractAsync(api, archivePath, destinationDirectory, options);

        var extractionException = Assert.IsType<ExtractionException>(exception);
        Assert.Contains("symbolic link whose target is outside", extractionException.Message);
        Assert.Equal(0, handlerCalls);
        Assert.False(File.Exists(Path.Combine(outsideDirectory, "secret.txt")));
    }

    [Theory]
    [InlineData("ReaderAll")]
    [InlineData("Archive")]
    [InlineData("AsyncReaderAll")]
    [InlineData("AsyncArchive")]
    public async Task EntriesBeneathSymbolicLink_ShouldNotBeExtracted(string api)
    {
        var destinationDirectory = GetScratchPath("extract");
        var targetDirectory = Path.Combine(destinationDirectory, "target");
        var archivePath = GetScratch2Path("symbolic-link-inside.tar");
        Directory.CreateDirectory(destinationDirectory);
        Directory.CreateDirectory(targetDirectory);
        BuildTar(archivePath, "target");

        var handlerCalls = 0;
        var options = new ExtractionOptions
        {
            SymbolicLinkHandler = (linkPath, linkTarget) =>
            {
                handlerCalls++;
                CreateReparsePoint(linkPath, linkTarget);
            },
        };

        var extractionException = (
            await ExtractAsync(api, archivePath, destinationDirectory, options)
        ).NotNull();

        Assert.Contains("symbolic link or reparse point", extractionException.ToString());
        Assert.Equal(1, handlerCalls);
        Assert.False(File.Exists(Path.Combine(targetDirectory, "secret.txt")));
    }

    private static void CreateReparsePoint(string linkPath, string linkTarget)
    {
        if (OperatingSystem.IsWindows())
        {
            // Directory junctions are reparse points that need no elevation, unlike symbolic links.
            // Junction targets must be absolute.
            var absoluteTarget = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(linkPath).NotNull(), linkTarget)
            );
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("mklink");
            startInfo.ArgumentList.Add("/j");
            startInfo.ArgumentList.Add(linkPath);
            startInfo.ArgumentList.Add(absoluteTarget);

            using var process = Process.Start(startInfo).NotNull();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            Directory.CreateSymbolicLink(linkPath, linkTarget);
        }
    }

    private static void BuildTar(string path, string linkTarget)
    {
        using var stream = File.Create(path);
        WriteTarEntry(stream, "link", (byte)'2', linkTarget, Array.Empty<byte>());
        WriteTarEntry(stream, "link/secret.txt", (byte)'0', null, Encoding.UTF8.GetBytes("secret"));
        stream.Write(new byte[TarBlockSize * 2]);
    }

    private static void WriteTarEntry(
        Stream stream,
        string name,
        byte entryType,
        string? linkTarget,
        byte[] data
    )
    {
        var header = new byte[TarBlockSize];
        WriteString(header, 0, 100, name);
        WriteOctal(header, 100, 8, 0b110_100_100);
        WriteOctal(header, 108, 8, 0);
        WriteOctal(header, 116, 8, 0);
        WriteOctal(header, 124, 12, data.Length);
        WriteOctal(header, 136, 12, 0);
        Array.Fill(header, (byte)' ', 148, 8);
        header[156] = entryType;
        WriteString(header, 157, 100, linkTarget ?? string.Empty);
        WriteString(header, 257, 6, "ustar");
        WriteString(header, 263, 2, "00");

        var checksum = header.Sum(value => value);
        WriteString(header, 148, 6, Convert.ToString(checksum, 8).PadLeft(6, '0'));
        header[154] = 0;
        header[155] = (byte)' ';

        stream.Write(header);
        stream.Write(data);

        var padding = (TarBlockSize - (data.Length % TarBlockSize)) % TarBlockSize;
        if (padding > 0)
        {
            stream.Write(new byte[padding]);
        }
    }

    private static void WriteString(byte[] buffer, int offset, int length, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
    }

    private static void WriteOctal(byte[] buffer, int offset, int length, long value)
    {
        WriteString(
            buffer,
            offset,
            length - 1,
            Convert.ToString(value, 8).PadLeft(length - 1, '0')
        );
        buffer[offset + length - 1] = 0;
    }

    private static Task<Exception?> ExtractAsync(
        string api,
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    ) =>
        api switch
        {
            "ReaderAll" => Task.FromResult(
                RecordException(() =>
                    ExtractWithReaderAll(archivePath, destinationDirectory, options)
                )
            ),
            "ReaderEntry" => Task.FromResult(
                RecordException(() =>
                    ExtractWithReaderEntry(archivePath, destinationDirectory, options)
                )
            ),
            "Archive" => Task.FromResult(
                RecordException(() =>
                    ExtractWithArchive(archivePath, destinationDirectory, options)
                )
            ),
            "ArchiveEntry" => Task.FromResult(
                RecordException(() =>
                    ExtractWithArchiveEntry(archivePath, destinationDirectory, options)
                )
            ),
            "AsyncReaderAll" => RecordExceptionAsync(() =>
                ExtractWithAsyncReaderAllAsync(archivePath, destinationDirectory, options)
            ),
            "AsyncReaderEntry" => RecordExceptionAsync(() =>
                ExtractWithAsyncReaderEntryAsync(archivePath, destinationDirectory, options)
            ),
            "AsyncArchive" => RecordExceptionAsync(() =>
                ExtractWithAsyncArchiveAsync(archivePath, destinationDirectory, options)
            ),
            "AsyncArchiveEntry" => RecordExceptionAsync(() =>
                ExtractWithAsyncArchiveEntryAsync(archivePath, destinationDirectory, options)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(api), api, null),
        };

    private static Exception? RecordException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> RecordExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void ExtractWithReaderAll(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        reader.WriteAllToDirectory(destinationDirectory, options);
    }

    private static void ExtractWithReaderEntry(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        Assert.True(reader.MoveToNextEntry());
        reader.WriteEntryToDirectory(destinationDirectory, options);
    }

    private static void ExtractWithArchive(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        archive.WriteToDirectory(destinationDirectory, options);
    }

    private static void ExtractWithArchiveEntry(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        archive.Entries.First().WriteToDirectory(destinationDirectory, options);
    }

    private static async Task ExtractWithAsyncReaderAllAsync(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);
        await reader.WriteAllToDirectoryAsync(destinationDirectory, options);
    }

    private static async Task ExtractWithAsyncReaderEntryAsync(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);
        Assert.True(await reader.MoveToNextEntryAsync());
        await reader.WriteEntryToDirectoryAsync(destinationDirectory, options);
    }

    private static async Task ExtractWithAsyncArchiveAsync(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        await using var archive = await ArchiveFactory.OpenAsyncArchive(archivePath);
        await archive.WriteToDirectoryAsync(destinationDirectory, options);
    }

    private static async Task ExtractWithAsyncArchiveEntryAsync(
        string archivePath,
        string destinationDirectory,
        ExtractionOptions options
    )
    {
        await using var archive = await ArchiveFactory.OpenAsyncArchive(archivePath);

        await foreach (var entry in archive.EntriesAsync)
        {
            await entry.WriteToDirectoryAsync(destinationDirectory, options);
            return;
        }

        throw new InvalidOperationException("Archive did not contain an entry.");
    }
}
#endif
