using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Crypto;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

[Collection(LargeArchiveCollection.Name)]
public class LargeArchiveTests : TestBase
{
    private const long LargeFileSize = 64L * 1024 * 1024;
    private const uint LargeFileCrc = 0xF9081EB0;
    private const int BufferSize = 64 * 1024;

    [Theory]
    [InlineData("Large/Large.zip")]
    [InlineData("Large/Large.tar")]
    [InlineData("Large/Large.gz")]
    [InlineData("Large/Large.rar")]
    [InlineData("Large/Large.7z")]
    public void OpenArchive_ShouldStreamLargeEntry(string fixtureName)
    {
        using var stream = File.OpenRead(GetMaterializedFixturePath(fixtureName));
        using var archive = ArchiveFactory.OpenArchive(stream);

        VerifyArchive(archive);
    }

    [Theory]
    [InlineData("Large/Large.zip")]
    [InlineData("Large/Large.tar")]
    [InlineData("Large/Large.gz")]
    [InlineData("Large/Large.rar")]
    [InlineData("Large/Large.7z")]
    public async Task OpenAsyncArchive_ShouldStreamLargeEntry(string fixtureName)
    {
        await using var stream = new AsyncOnlyStream(
            File.OpenRead(await GetMaterializedFixturePathAsync(fixtureName))
        );
        await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);

        var entry = await GetSingleEntryAsync(archive);

        var entryStream = await entry.OpenEntryStreamAsync();
        await using var entryStreamScope = entryStream.DisposeAsyncScope();
        await VerifyContentAsync(entry.Key, entryStream);
    }

    [Theory]
    [InlineData("Large/Large.zip")]
    [InlineData("Large/Large.tar")]
    [InlineData("Large/Large.gz")]
    [InlineData("Large/Large.rar")]
    [InlineData("Large/Large.tar.gz")]
    public void OpenReader_ShouldStreamLargeEntry(string fixtureName)
    {
        using var stream = File.OpenRead(GetMaterializedFixturePath(fixtureName));
        using var reader = ReaderFactory.OpenReader(stream);

        VerifyReader(reader);
    }

    [Theory]
    [InlineData("Large/Large.zip")]
    [InlineData("Large/Large.tar")]
    [InlineData("Large/Large.gz")]
    [InlineData("Large/Large.rar")]
    [InlineData("Large/Large.tar.gz")]
    public async Task OpenAsyncReader_ShouldStreamLargeEntry(string fixtureName)
    {
        await using var stream = new AsyncOnlyStream(
            File.OpenRead(await GetMaterializedFixturePathAsync(fixtureName))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);

        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.False(reader.Entry.IsDirectory);

        await using var entryStream = await reader.OpenEntryStreamAsync();
        await VerifyContentAsync(reader.Entry.Key, entryStream);
        Assert.False(await reader.MoveToNextEntryAsync());
    }

    private static string GetFixturePath(string fixtureName) =>
        Path.Combine(TEST_ARCHIVES_PATH, fixtureName);

    private string GetMaterializedFixturePath(string fixtureName) =>
        fixtureName == "Large/Large.tar" ? MaterializeTarFixture() : GetFixturePath(fixtureName);

    private async Task<string> GetMaterializedFixturePathAsync(string fixtureName) =>
        fixtureName == "Large/Large.tar"
            ? await MaterializeTarFixtureAsync()
            : GetFixturePath(fixtureName);

    private string MaterializeTarFixture()
    {
        var tarPath = Path.Combine(SCRATCH_FILES_PATH, "Large.tar");
        using var compressedStream = File.OpenRead(GetFixturePath("Large/Large.tar.gz"));
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var tarStream = File.Create(tarPath);
        gzipStream.CopyTo(tarStream);
        return tarPath;
    }

    private async Task<string> MaterializeTarFixtureAsync()
    {
        var tarPath = Path.Combine(SCRATCH_FILES_PATH, "Large.tar");
        using var compressedStream = File.OpenRead(GetFixturePath("Large/Large.tar.gz"));
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var tarStream = File.Create(tarPath);
        await gzipStream.CopyToAsync(tarStream);
        return tarPath;
    }

    private static void VerifyArchive(IArchive archive)
    {
        var entry = Assert.Single(archive.Entries);

        Assert.False(entry.IsDirectory);
        using var entryStream = entry.OpenEntryStream();
        VerifyContent(entry.Key, entryStream);
    }

    private static async Task<IArchiveEntry> GetSingleEntryAsync(IAsyncArchive archive)
    {
        IArchiveEntry? entry = null;
        await foreach (var candidate in archive.EntriesAsync)
        {
            Assert.Null(entry);
            entry = candidate;
        }

        return entry ?? throw new InvalidOperationException("The archive contains no entries.");
    }

    private static void VerifyReader(IReader reader)
    {
        Assert.True(reader.MoveToNextEntry());
        Assert.False(reader.Entry.IsDirectory);
        using var entryStream = reader.OpenEntryStream();
        VerifyContent(reader.Entry.Key, entryStream);
        Assert.False(reader.MoveToNextEntry());
    }

    private static void VerifyContent(string? key, Stream entryStream)
    {
        Assert.Equal("large.bin", key);

        using var crcStream = new Crc32Stream(Stream.Null);
        var buffer = new byte[BufferSize];
        long length = 0;
        int bytesRead;
        while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            crcStream.Write(buffer, 0, bytesRead);
            length += bytesRead;
        }

        Assert.Equal(LargeFileSize, length);
        Assert.Equal(LargeFileCrc, crcStream.Crc);
    }

    private static async Task VerifyContentAsync(string? key, Stream entryStream)
    {
        Assert.Equal("large.bin", key);

        using var crcStream = new Crc32Stream(Stream.Null);
        var buffer = new byte[BufferSize];
        long length = 0;
        int bytesRead;
        while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await crcStream.WriteAsync(buffer, 0, bytesRead);
            length += bytesRead;
        }

        Assert.Equal(LargeFileSize, length);
        Assert.Equal(LargeFileCrc, crcStream.Crc);
    }
}

[CollectionDefinition(LargeArchiveCollection.Name, DisableParallelization = true)]
public sealed class LargeArchiveCollection
{
    public const string Name = "Large archive fixtures";
}
