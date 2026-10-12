using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Writers.SevenZip;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipWriterStreamlessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipWriter_Streamless_ClosesOwnedStream(bool async)
    {
        using var output = new MemoryStream();
        var writer = new SevenZipWriter(
            output,
            new SevenZipWriterOptions { LeaveStreamOpen = false }
        );
        writer.WriteDirectory("dir", null);
        if (async)
        {
            await writer.DisposeAsync();
        }
        else
        {
            writer.Dispose();
        }
        Assert.False(output.CanRead);
        using var input = new MemoryStream(output.ToArray());
        using var archive = SevenZipArchive.OpenArchive(input);
        Assert.True(archive.Entries.Single().IsDirectory);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 2)]
    public async Task SevenZipWriter_Streamless_RoundTrip(
        bool async,
        bool compressHeader,
        int directories
    )
    {
        using var output = new MemoryStream();
        var writer = new SevenZipWriter(
            output,
            new SevenZipWriterOptions { CompressHeader = compressHeader }
        );
        for (var i = 0; i < directories; i++)
        {
            if (async)
            {
                await writer.WriteDirectoryAsync($"dir{i}", null, CancellationToken.None);
            }
            else
            {
                writer.WriteDirectory($"dir{i}", null);
            }
        }
        if (async)
        {
            await writer.DisposeAsync();
        }
        else
        {
            writer.Dispose();
        }

        Assert.True(output.CanRead);
        var bytes = output.ToArray();
        var offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(12));
        var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(20));
        Assert.Equal((ulong)bytes.Length, 32 + offset + size);
        output.Position = 0;
        using var archive = SevenZipArchive.OpenArchive(output);
        var entries = archive.Entries.ToList();
        Assert.Equal(directories, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.IsDirectory));
    }
}
