using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Ahead-of-time WinZip AES key derivation (ReaderOptions.EnableParallelism) and batched AES-CTR
/// decryption must produce exactly the same entries as sequential reading.
/// </summary>
public class ZipWinzipAesReaderTests : TestBase
{
    private const string ManyEntries = "Zip.many.aes256.datadescriptor.zip";

    public static TheoryData<string, string> AesArchives =>
        new()
        {
            { "Zip.deflate.aes128.datadescriptor.zip", "test" },
            { "Zip.deflate.aes192.datadescriptor.zip", "test" },
            { "Zip.deflate.aes256.datadescriptor.zip", "test" },
            { "Zip.store.aes256.datadescriptor.zip", "test" },
            { ManyEntries, "test" },
            { "Zip.deflate.WinzipAES.zip", "test" },
            { "Zip.deflate.WinzipAES2.zip", "test" },
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reader_Reads_Many_Entries(bool enableParallelism)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, ManyEntries));
        using var reader = ZipReader.OpenReader(stream, Options("test", enableParallelism));

        var files = 0;
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                files++;
                Assert.Equal(ExpectedManyEntry(reader.Entry.Key!), Read(reader));
            }
        }

        Assert.Equal(40, files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async ValueTask Reader_Reads_Many_Entries_Async(bool enableParallelism)
    {
        using Stream stream = new AsyncOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, ManyEntries))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            stream,
            Options("test", enableParallelism)
        );

        var files = 0;
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                files++;
                Assert.Equal(ExpectedManyEntry(reader.Entry.Key!), await ReadAsync(reader));
            }
        }

        Assert.Equal(40, files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async ValueTask Reader_Reads_Many_Entries_When_Some_Are_Skipped_Async(
        bool enableParallelism
    )
    {
        using Stream stream = new AsyncOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, ManyEntries))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            stream,
            Options("test", enableParallelism)
        );

        var files = 0;
        var read = 0;
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory && files++ % 3 == 0)
            {
                // keys derived ahead for the skipped entries are never claimed
                read++;
                Assert.Equal(ExpectedManyEntry(reader.Entry.Key!), await ReadAsync(reader));
            }
        }

        Assert.Equal(40, files);
        Assert.Equal(14, read);
    }

    [Theory]
    [MemberData(nameof(AesArchives))]
    public async ValueTask Parallel_Key_Derivation_Matches_Sequential_Reading(
        string archive,
        string password
    )
    {
        var sequential = ReadAll(archive, Options(password, enableParallelism: false));
        var parallel = ReadAll(archive, Options(password, enableParallelism: true));
        var parallelAsync = await ReadAllAsync(archive, Options(password, enableParallelism: true));

        Assert.NotEmpty(sequential);
        AssertSame(sequential, parallel);
        AssertSame(sequential, parallelAsync);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async ValueTask Wrong_Password_Is_Rejected_With_Parallel_Key_Derivation(bool async)
    {
        var options = Options("wrong", enableParallelism: true);
        if (async)
        {
            using Stream stream = new AsyncOnlyStream(
                File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, ManyEntries))
            );
            await using var reader = (IAsyncReader)ZipReader.OpenReader(stream, options);
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        await ReadAsync(reader);
                    }
                }
            });
        }
        else
        {
            using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, ManyEntries));
            using var reader = ZipReader.OpenReader(stream, options);
            Assert.Throws<InvalidFormatException>(() =>
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Read(reader);
                    }
                }
            });
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(65536)]
    public async ValueTask Aes_Entry_Reads_Identically_With_Any_Buffer_Size(int bufferSize)
    {
        // Stored entries expose the decrypted stream directly; 20000 bytes span several keystream batches.
        var expected = ZipEncryptedDataDescriptorTests.Binary(20000);
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.store.aes256.datadescriptor.zip");

        using (var zip = ZipArchive.OpenArchive(path, Options("test", false)))
        {
            var entry = zip.Entries.Single(e => e.Key == "data/sub/binary.bin");
            using var entryStream = entry.OpenEntryStream();
            Assert.Equal(expected, ReadInChunks(entryStream, bufferSize));
        }

        using Stream stream = new AsyncOnlyStream(File.OpenRead(path));
        await using var asyncZip = await ZipArchive.OpenAsyncArchive(
            stream,
            Options("test", false)
        );
        await foreach (var entry in asyncZip.EntriesAsync)
        {
            if (entry.Key != "data/sub/binary.bin")
            {
                continue;
            }

            var entryStream = await entry.OpenEntryStreamAsync();
            await using var scope = entryStream.DisposeAsyncScope().ConfigureAwait(false);
            Assert.Equal(expected, await ReadInChunksAsync(entryStream, bufferSize));
        }
    }

    private static ReaderOptions Options(string password, bool enableParallelism) =>
        new() { Password = password, EnableParallelism = enableParallelism };

    private static byte[] ExpectedManyEntry(string key)
    {
        // many/fileNN.bin => Binary(1000 + NN * 37), see the fixture generator
        var index = int.Parse(Path.GetFileNameWithoutExtension(key).Substring("file".Length));
        return ZipEncryptedDataDescriptorTests.Binary(1000 + (index * 37));
    }

    private static Dictionary<string, byte[]> ReadAll(string archive, ReaderOptions options)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive));
        using var reader = ZipReader.OpenReader(stream, options);
        var contents = new Dictionary<string, byte[]>();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                contents.Add(reader.Entry.Key!, Read(reader));
            }
        }
        return contents;
    }

    private static async ValueTask<Dictionary<string, byte[]>> ReadAllAsync(
        string archive,
        ReaderOptions options
    )
    {
        using Stream stream = new AsyncOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive))
        );
        await using var reader = (IAsyncReader)ZipReader.OpenReader(stream, options);
        var contents = new Dictionary<string, byte[]>();
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                contents.Add(reader.Entry.Key!, await ReadAsync(reader));
            }
        }
        return contents;
    }

    private static void AssertSame(
        Dictionary<string, byte[]> expected,
        Dictionary<string, byte[]> actual
    )
    {
        Assert.Equal(expected.Keys.OrderBy(k => k), actual.Keys.OrderBy(k => k));
        foreach (var entry in expected)
        {
            Assert.True(
                entry.Value.SequenceEqual(actual[entry.Key]),
                $"content mismatch for {entry.Key}"
            );
        }
    }

    private static byte[] ReadInChunks(Stream stream, int bufferSize)
    {
        var buffer = new byte[bufferSize];
        using var output = new MemoryStream();
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static async ValueTask<byte[]> ReadInChunksAsync(Stream stream, int bufferSize)
    {
        var buffer = new byte[bufferSize];
        using var output = new MemoryStream();
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static byte[] Read(IReader reader)
    {
        using var entryStream = reader.OpenEntryStream();
        using var output = new MemoryStream();
        entryStream.CopyTo(output);
        return output.ToArray();
    }

    private static async ValueTask<byte[]> ReadAsync(IAsyncReader reader)
    {
        var entryStream = await reader.OpenEntryStreamAsync();
        await using var scope = entryStream.DisposeAsyncScope().ConfigureAwait(false);
        using var output = new MemoryStream();
        await entryStream.CopyToAsync(output);
        return output.ToArray();
    }
}
