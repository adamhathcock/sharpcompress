using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.SevenZip;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipWriterEncryptionTests : TestBase
{
    private const string Password = "secret pässword";

    // Not a multiple of 16 bytes and larger than the encoder's 4 KB buffer, so the final
    // partial AES block and multi-chunk writes are both exercised.
    private static readonly byte[] LargeContent = CreateRandom(100_003);

    private static readonly byte[] SmallContent = Encoding.UTF8.GetBytes("small file");

    [Theory]
    [InlineData(CompressionType.LZMA2, false, false)]
    [InlineData(CompressionType.LZMA2, true, false)]
    [InlineData(CompressionType.LZMA, false, false)]
    [InlineData(CompressionType.LZMA, true, false)]
    [InlineData(CompressionType.LZMA2, false, true)]
    [InlineData(CompressionType.LZMA2, true, true)]
    public async ValueTask SevenZipWriter_Encrypted_RoundTrip(
        CompressionType compressionType,
        bool encryptHeader,
        bool useAsync
    )
    {
        var archive = await WriteArchive(
            new SevenZipWriterOptions(compressionType)
            {
                Password = Password,
                EncryptHeader = encryptHeader,
            },
            useAsync
        );

        using var reader = SevenZipArchive.OpenArchive(
            new MemoryStream(archive),
            new ReaderOptions { Password = Password }
        );
        var entries = reader.Entries.ToDictionary(entry => entry.Key!);
        Assert.Equal(4, entries.Count);
        Assert.True(entries["dir"].IsDirectory);

        Assert.True(entries["dir/large.bin"].IsEncrypted);
        // SharpCompress reports both LZMA and LZMA2 as LZMA; the AES filter must not hide it.
        Assert.Equal(CompressionType.LZMA, entries["dir/large.bin"].CompressionType);
        Assert.Equal(LargeContent, Read(entries["dir/large.bin"]));
        Assert.True(entries["small.txt"].IsEncrypted);
        Assert.Equal(SmallContent, Read(entries["small.txt"]));

        // Empty files have no data stream, so there is nothing to encrypt (as in 7-Zip).
        Assert.False(entries["empty.txt"].IsEncrypted);
        Assert.Empty(Read(entries["empty.txt"]));
    }

    [Fact]
    public async ValueTask SevenZipWriter_Encrypted_ReadsWithForwardOnlyReader()
    {
        var archive = await WriteArchive(
            new SevenZipWriterOptions { Password = Password, EncryptHeader = true },
            useAsync: false
        );

        using var archiveReader = SevenZipArchive.OpenArchive(
            new MemoryStream(archive),
            new ReaderOptions { Password = Password }
        );
        using var reader = archiveReader.ExtractAllEntries();
        var contents = new Dictionary<string, byte[]>();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                using var output = new MemoryStream();
                reader.WriteEntryTo(output);
                contents[reader.Entry.Key!] = output.ToArray();
            }
        }

        Assert.Equal(LargeContent, contents["dir/large.bin"]);
        Assert.Equal(SmallContent, contents["small.txt"]);
        Assert.Empty(contents["empty.txt"]);
    }

    [Fact]
    public async ValueTask SevenZipWriter_EncryptedData_ListsWithoutPasswordButCannotRead()
    {
        var archive = await WriteArchive(
            new SevenZipWriterOptions { Password = Password },
            useAsync: false
        );

        using var reader = SevenZipArchive.OpenArchive(new MemoryStream(archive));
        var large = reader.Entries.Single(entry => entry.Key == "dir/large.bin");
        Assert.Equal(LargeContent.Length, large.Size);
        Assert.Throws<CryptographicException>(() => Read(large));
    }

    [Fact]
    public async ValueTask SevenZipWriter_EncryptedHeader_HidesEntriesWithoutPassword()
    {
        var archive = await WriteArchive(
            new SevenZipWriterOptions { Password = Password, EncryptHeader = true },
            useAsync: false
        );

        Assert.Throws<CryptographicException>(() =>
        {
            using var reader = SevenZipArchive.OpenArchive(new MemoryStream(archive));
            _ = reader.Entries.ToList();
        });

        // The names are not stored in plain text anywhere in the file.
        Assert.DoesNotContain("large.bin", Encoding.Unicode.GetString(archive));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async ValueTask SevenZipWriter_Encrypted_WrongPasswordFails(bool encryptHeader)
    {
        var archive = await WriteArchive(
            new SevenZipWriterOptions { Password = Password, EncryptHeader = encryptHeader },
            useAsync: false
        );

        Assert.ThrowsAny<CryptographicException>(() =>
        {
            using var reader = SevenZipArchive.OpenArchive(
                new MemoryStream(archive),
                new ReaderOptions { Password = "wrong" }
            );
            foreach (var entry in reader.Entries.Where(entry => !entry.IsDirectory))
            {
                Read(entry);
            }
        });
    }

    [Fact]
    public void SevenZipWriter_EncryptedHeader_DirectoryOnlyArchive()
    {
        using var archiveStream = new MemoryStream();
        using (
            var writer = new SevenZipWriter(
                archiveStream,
                new SevenZipWriterOptions { Password = Password, EncryptHeader = true }
            )
        )
        {
            writer.WriteDirectory("a", DateTime.UtcNow);
            writer.WriteDirectory("a/b", DateTime.UtcNow);
        }

        archiveStream.Position = 0;
        using var reader = SevenZipArchive.OpenArchive(
            archiveStream,
            new ReaderOptions { Password = Password, LeaveStreamOpen = true }
        );
        Assert.Equal(
            new[] { "a", "a/b" },
            reader.Entries.Select(entry => entry.Key!.TrimEnd('/')).OrderBy(key => key)
        );
    }

    [Fact]
    public async ValueTask SevenZipWriter_Encrypted_UsesFreshInitializationVectors()
    {
        var options = new SevenZipWriterOptions { Password = Password };
        var first = await WriteArchive(options, useAsync: false);
        var second = await WriteArchive(options, useAsync: false);

        // Same input, password and settings: only the random IVs differ.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SevenZipWriter_EncryptHeaderWithoutPassword_Throws()
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentException>(() =>
            new SevenZipWriter(output, new SevenZipWriterOptions { EncryptHeader = true })
        );
    }

    [Fact]
    public void SevenZipWriter_EmptyPassword_Throws()
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentException>(() =>
            new SevenZipWriter(output, new SevenZipWriterOptions { Password = "" })
        );
    }

    private static async ValueTask<byte[]> WriteArchive(
        SevenZipWriterOptions options,
        bool useAsync
    )
    {
        using var archiveStream = new MemoryStream();
        if (useAsync)
        {
            await using var writer = new SevenZipWriter(
                new AsyncOnlyStream(archiveStream),
                options
            );
            await writer.WriteDirectoryAsync("dir", DateTime.UtcNow);
            await writer.WriteAsync(
                "dir/large.bin",
                new MemoryStream(LargeContent),
                DateTime.UtcNow
            );
            await writer.WriteAsync("small.txt", new MemoryStream(SmallContent), DateTime.UtcNow);
            await writer.WriteAsync("empty.txt", new MemoryStream(), DateTime.UtcNow);
        }
        else
        {
            using var writer = new SevenZipWriter(archiveStream, options);
            writer.WriteDirectory("dir", DateTime.UtcNow);
            writer.Write("dir/large.bin", new MemoryStream(LargeContent), DateTime.UtcNow);
            writer.Write("small.txt", new MemoryStream(SmallContent), DateTime.UtcNow);
            writer.Write("empty.txt", new MemoryStream(), DateTime.UtcNow);
        }

        return archiveStream.ToArray();
    }

    private static byte[] Read(IArchiveEntry entry)
    {
        using var output = new MemoryStream();
        using (var content = entry.OpenEntryStream())
        {
            content.CopyTo(output);
        }
        return output.ToArray();
    }

    private static byte[] CreateRandom(int length)
    {
        var bytes = new byte[length];
        new Random(1234).NextBytes(bytes);
        return bytes;
    }
}
