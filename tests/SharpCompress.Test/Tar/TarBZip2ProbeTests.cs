using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarBZip2ProbeTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task LargeIncompressibleBlock_IsDetectedAndExtracted(
        bool async,
        bool forwardOnly,
        bool directTarReader
    )
    {
        var expected = new byte[1_000_000];
        new Random(1486).NextBytes(expected);
        using var archive = new MemoryStream();
        using (
            var writer = new TarWriter(archive, new TarWriterOptions(CompressionType.BZip2, true))
        )
        using (var content = new MemoryStream(expected))
        {
            writer.Write("file.bin", content, null);
        }

        // Ensure this exercises compressed data larger than the former 900,000-byte limit.
        Assert.True(archive.Length > 900_000);
        archive.Position = 0;
        using Stream input = forwardOnly ? new ForwardOnlyStream(archive) : archive;
        using var actual = new MemoryStream();
        var options = ReaderOptions.ForExternalStream;
        if (async)
        {
            await using var reader = directTarReader
                ? await TarReader.OpenAsyncReader(input, options, CancellationToken.None)
                : await ReaderFactory.OpenAsyncReader(input, options, CancellationToken.None);
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal("file.bin", reader.Entry.Key);
            using (var entry = await reader.OpenEntryStreamAsync(CancellationToken.None))
            {
                await entry.CopyToAsync(actual, 81920, CancellationToken.None);
            }
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = directTarReader
                ? TarReader.OpenReader(input, options)
                : ReaderFactory.OpenReader(input, options);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("file.bin", reader.Entry.Key);
            using (var entry = reader.OpenEntryStream())
            {
                entry.CopyTo(actual);
            }
            Assert.False(reader.MoveToNextEntry());
        }
        Assert.Equal(expected, actual.ToArray());
        Assert.True(input.CanRead);
    }
}
