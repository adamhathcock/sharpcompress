using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipDeflateSolidTests : TestBase
{
    private static readonly int[] Sizes = [1000, 70000, 100000, 200000, 500000, 1048576, 3000000];

    // Generated with libarchive bsdtar --format 7zip --options compression=deflate.
    // Each file repeats a 4096-byte xorshift sequence with a different starting offset.
    private static byte[] CreateExpected(int index)
    {
        var block = new byte[4096];
        uint state = 0x12345678;
        for (var i = 0; i < block.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            block[i] = (byte)state;
        }

        var data = new byte[Sizes[index]];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = block[(i + index * 997) % block.Length];
        }
        return data;
    }

    [Theory]
    [InlineData(512, 0)]
    [InlineData(4096, 1)]
    [InlineData(65536, 2)]
    [InlineData(131072, 0)]
    [InlineData(300000, 0)]
    public void SevenZipArchive_Solid_Deflate_ExtractAllEntries(int bufferSize, int firstEntryMode)
    {
        using var archive = SevenZipArchive.OpenArchive(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.Deflate.7z")
        );
        Assert.True(archive.IsSolid);
        using var reader = archive.ExtractAllEntries();
        var index = 0;
        while (reader.MoveToNextEntry())
        {
            Assert.Equal($"{index}.bin", reader.Entry.Key);
            Assert.Equal(Sizes[index], reader.Entry.Size);
            if (index == 0 && firstEntryMode == 2)
            {
                index++;
                continue;
            }

            using var stream = reader.OpenEntryStream();
            if (index == 0 && firstEntryMode == 1)
            {
                Assert.True(stream.Read(new byte[17], 0, 17) > 0);
            }
            else
            {
                using var output = new MemoryStream();
                stream.CopyTo(output, bufferSize);
                Assert.Equal(CreateExpected(index), output.ToArray());
            }
            index++;
        }
        Assert.Equal(Sizes.Length, index);
    }

    [Theory]
    [InlineData(512, 0)]
    [InlineData(4096, 1)]
    [InlineData(65536, 2)]
    [InlineData(131072, 0)]
    [InlineData(300000, 0)]
    public async Task SevenZipArchive_Solid_Deflate_ExtractAllEntries_Async(
        int bufferSize,
        int firstEntryMode
    )
    {
        await using var archive = await SevenZipArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.Deflate.7z"),
            cancellationToken: CancellationToken.None
        );
        Assert.True(await archive.IsSolidAsync());
        await using var reader = await archive.ExtractAllEntriesAsync();
        var index = 0;
        while (await reader.MoveToNextEntryAsync(CancellationToken.None))
        {
            Assert.Equal($"{index}.bin", reader.Entry.Key);
            Assert.Equal(Sizes[index], reader.Entry.Size);
            if (index == 0 && firstEntryMode == 2)
            {
                index++;
                continue;
            }

            await using var stream = await reader.OpenEntryStreamAsync(CancellationToken.None);
            if (index == 0 && firstEntryMode == 1)
            {
                Assert.True(
                    await stream.ReadAsync(new byte[17], 0, 17, CancellationToken.None) > 0
                );
            }
            else
            {
                using var output = new MemoryStream();
                await stream.CopyToAsync(output, bufferSize, CancellationToken.None);
                Assert.Equal(CreateExpected(index), output.ToArray());
            }
            index++;
        }
        Assert.Equal(Sizes.Length, index);
    }
}
