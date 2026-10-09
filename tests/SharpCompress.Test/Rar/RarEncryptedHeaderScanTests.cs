using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarEncryptedHeaderScanTests : TestBase
{
    [Theory]
    [InlineData("Rar.encrypted_filesAndHeader.rar", false)]
    [InlineData("Rar.encrypted_filesAndHeader.rar", true)]
    [InlineData("Rar5.encrypted_filesAndHeader.rar", false)]
    [InlineData("Rar5.encrypted_filesAndHeader.rar", true)]
    public async Task Rar_EncryptedHeaders_Rescan(string filename, bool useAsync)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        var factory = new RarHeaderFactory(
            StreamingMode.Seekable,
            ReaderOptions.ForExternalStream.WithPassword("test")
        );
        var expected = await ReadNames(false);
        Assert.Equal(6, expected.Count);
        stream.Position = 0;
        Assert.Single(await ReadNames(true));
        stream.Position = 0;
        Assert.Equal(expected, await ReadNames(false));

        async Task<List<string>> ReadNames(bool stopAfterFirst)
        {
            var names = new List<string>();
            if (useAsync)
            {
                await foreach (var header in factory.ReadHeadersAsync(stream))
                {
                    if (header is FileHeader file && header.HeaderType == HeaderType.File)
                    {
                        Assert.NotNull(file.FileName);
                        names.Add(file.FileName);
                        if (stopAfterFirst)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var header in factory.ReadHeaders(stream))
                {
                    if (header is FileHeader file && header.HeaderType == HeaderType.File)
                    {
                        Assert.NotNull(file.FileName);
                        names.Add(file.FileName);
                        if (stopAfterFirst)
                        {
                            break;
                        }
                    }
                }
            }
            return names;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rar5_Solid_EncryptedHeaders_ExtractAll(bool readMetadataFirst)
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.encrypted_headers.rar")
        );
        using var archive = RarArchive.OpenArchive(
            stream,
            ReaderOptions.ForExternalStream.WithPassword("test")
        );
        if (readMetadataFirst)
        {
            Assert.True(archive.IsSolid);
            Assert.Equal(3, archive.Entries.Count());
        }
        using var reader = archive.ExtractAllEntries();
        var count = 0;
        while (reader.MoveToNextEntry())
        {
            Assert.Equal($"f{count:D3}.txt", reader.Entry.Key);
            using var entryStream = reader.OpenEntryStream();
            using var textReader = new StreamReader(entryStream);
            Assert.Equal("Solid encrypted header regression test.\n", textReader.ReadToEnd());
            count++;
        }
        Assert.Equal(3, count);
        Assert.Equal(3, archive.Entries.Count());
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar5_Solid_EncryptedHeaders_ExtractAll_Async(bool readMetadataFirst)
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.encrypted_headers.rar")
        );
        await using var archive = await RarArchive.OpenAsyncArchive(
            stream,
            ReaderOptions.ForExternalStream.WithPassword("test"),
            CancellationToken.None
        );
        if (readMetadataFirst)
        {
            Assert.True(await archive.IsSolidAsync());
            Assert.Equal(3, (await archive.EntriesAsync.ToListAsync()).Count);
        }
        await using var reader = await archive.ExtractAllEntriesAsync();
        var count = 0;
        while (await reader.MoveToNextEntryAsync(CancellationToken.None))
        {
            Assert.Equal($"f{count:D3}.txt", reader.Entry.Key);
            using var entryStream = await reader.OpenEntryStreamAsync(CancellationToken.None);
            using var textReader = new StreamReader(entryStream);
            Assert.Equal(
                "Solid encrypted header regression test.\n",
                await textReader.ReadToEndAsync()
            );
            count++;
        }
        Assert.Equal(3, count);
        Assert.Equal(3, (await archive.EntriesAsync.ToListAsync()).Count);
        Assert.True(stream.CanRead);
    }
}
