using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Archives written by streaming encryptors (these fixtures by Aspose.Zip) encrypt every entry, including
/// directories, and set the post data descriptor flag with zero sizes in the local header. The sizes are
/// only in the data descriptor and the central directory.
/// </summary>
public class ZipEncryptedDataDescriptorTests : TestBase
{
    private const string Password = "test";

    public static TheoryData<string> Archives =>
        new()
        {
            "Zip.deflate.zipcrypto.datadescriptor.zip",
            "Zip.deflate.aes128.datadescriptor.zip",
            "Zip.deflate.aes192.datadescriptor.zip",
            "Zip.deflate.aes256.datadescriptor.zip",
            "Zip.store.zipcrypto.datadescriptor.zip",
            "Zip.store.aes256.datadescriptor.zip",
        };

    [Theory]
    [MemberData(nameof(Archives))]
    public void Reader_Reads_All_Entries(string archive)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive));
        using var reader = ZipReader.OpenReader(stream, Options());

        var contents = new Dictionary<string, byte[]>();
        while (reader.MoveToNextEntry())
        {
            contents.Add(reader.Entry.Key!, reader.Entry.IsDirectory ? [] : Read(reader));
        }

        AssertExpected(contents);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public async ValueTask Reader_Reads_All_Entries_Async(string archive)
    {
        using Stream stream = new AsyncOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(stream, Options());

        var contents = new Dictionary<string, byte[]>();
        while (await reader.MoveToNextEntryAsync())
        {
            contents.Add(
                reader.Entry.Key!,
                reader.Entry.IsDirectory ? [] : await ReadAsync(reader)
            );
        }

        AssertExpected(contents);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public void ReaderFactory_Reads_Entries_When_Some_Are_Skipped(string archive)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive));
        using var reader = ReaderFactory.OpenReader(stream, Options().WithLookForHeader(true));

        var keys = new List<string>();
        while (reader.MoveToNextEntry())
        {
            keys.Add(reader.Entry.Key!);
            if (reader.Entry.Key == "data/sub/binary.bin")
            {
                // partially read, the rest is skipped by the reader
                using var entryStream = reader.OpenEntryStream();
                Assert.Equal(1, entryStream.Read(new byte[1], 0, 1));
            }
            else if (reader.Entry.Key == "data/empty.txt")
            {
                Assert.Empty(Read(reader));
            }
            // data/readme.txt is never opened
        }

        Assert.Equal(ExpectedEntries.Keys.OrderBy(k => k), keys.OrderBy(k => k));
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public async ValueTask Reader_Reads_Entries_When_Some_Are_Skipped_Async(string archive)
    {
        using Stream stream = new AsyncOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(stream, Options());

        string? readmeKey = null;
        var count = 0;
        while (await reader.MoveToNextEntryAsync())
        {
            count++;
            if (reader.Entry.Key == "data/readme.txt")
            {
                readmeKey = reader.Entry.Key;
                Assert.Equal(ExpectedEntries[readmeKey], await ReadAsync(reader));
            }
            // every other entry is skipped without being opened
        }

        Assert.Equal(ExpectedEntries.Count, count);
        Assert.NotNull(readmeKey);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public void Reader_Reports_Sizes_From_Central_Directory(string archive)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive));
        using var reader = ZipReader.OpenReader(stream, Options());

        var files = 0;
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                files++;
                Assert.Equal(ExpectedEntries[reader.Entry.Key!].Length, reader.Entry.Size);
            }
        }

        Assert.Equal(3, files);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public void Archive_Reads_All_Entries(string archive)
    {
        using var zip = ZipArchive.OpenArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archive),
            Options()
        );

        var contents = new Dictionary<string, byte[]>();
        foreach (var entry in zip.Entries)
        {
            if (entry.IsDirectory)
            {
                contents.Add(entry.Key!, []);
                continue;
            }

            using var entryStream = entry.OpenEntryStream();
            using var output = new MemoryStream();
            entryStream.CopyTo(output);
            contents.Add(entry.Key!, output.ToArray());
        }

        AssertExpected(contents);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public async ValueTask Archive_Reads_All_Entries_Async(string archive)
    {
        using Stream stream = new AsyncOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive))
        );
        await using var zip = await ZipArchive.OpenAsyncArchive(stream, Options());

        var contents = new Dictionary<string, byte[]>();
        await foreach (var entry in zip.EntriesAsync)
        {
            if (entry.IsDirectory)
            {
                contents.Add(entry.Key!, []);
                continue;
            }

            var entryStream = await entry.OpenEntryStreamAsync();
            await using var scope = entryStream.DisposeAsyncScope().ConfigureAwait(false);
            using var output = new MemoryStream();
            await entryStream.CopyToAsync(output);
            contents.Add(entry.Key!, output.ToArray());
        }

        AssertExpected(contents);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public void Wrong_Password_Is_Rejected(string archive)
    {
        var options = Options() with { Password = "wrong" };
        var expected = archive.Contains("zipcrypto")
            ? typeof(CryptographicException)
            : typeof(InvalidFormatException);

        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive)))
        {
            using var reader = ZipReader.OpenReader(stream, options);
            var exception = Record.Exception(() =>
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Read(reader);
                    }
                }
            });
            Assert.IsType(expected, exception);
        }

        using var zip = ZipArchive.OpenArchive(Path.Combine(TEST_ARCHIVES_PATH, archive), options);
        var archiveException = Record.Exception(() =>
        {
            foreach (var entry in zip.Entries.Where(e => !e.IsDirectory))
            {
                using var entryStream = entry.OpenEntryStream();
                entryStream.CopyTo(Stream.Null);
            }
        });
        Assert.IsType(expected, archiveException);
    }

    [Theory]
    [MemberData(nameof(Archives))]
    public void NonSeekable_Stream_Is_Not_Supported(string archive)
    {
        // Without the central directory the encrypted entries cannot be delimited; this must fail
        // instead of silently ending the enumeration after the first entry.
        using Stream stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive))
        );
        using var reader = ZipReader.OpenReader(stream, Options());

        Assert.Throws<NotSupportedException>(() =>
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

    [Theory]
    [MemberData(nameof(Archives))]
    public async ValueTask NonSeekable_Stream_Is_Not_Supported_Async(string archive)
    {
        using Stream stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archive))
        );
        // ZipReader directly: ReaderFactory format detection would report the failure as an unknown format.
        await using var reader = (IAsyncReader)
            ZipReader.OpenReader(new AsyncOnlyStream(stream, disposeStream: false), Options());

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
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

    private static ReaderOptions Options() => new() { Password = Password };

    private static Dictionary<string, byte[]> ExpectedEntries =>
        new()
        {
            ["data/"] = [],
            ["data/readme.txt"] = Encoding.UTF8.GetBytes(
                string.Concat(
                    Enumerable
                        .Range(0, 100)
                        .Select(i => $"line {i}: the quick brown fox jumps over the lazy dog\n")
                )
            ),
            ["data/empty.txt"] = [],
            ["data/sub/"] = [],
            ["data/sub/binary.bin"] = Binary(20000),
        };

    internal static byte[] Binary(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)(((uint)i * 2654435761u) >> 24);
        }
        return bytes;
    }

    private static void AssertExpected(Dictionary<string, byte[]> contents)
    {
        var expected = ExpectedEntries;
        Assert.Equal(expected.Keys.OrderBy(k => k), contents.Keys.OrderBy(k => k));
        foreach (var entry in expected)
        {
            Assert.True(
                entry.Value.SequenceEqual(contents[entry.Key]),
                $"content mismatch for {entry.Key}"
            );
        }
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
