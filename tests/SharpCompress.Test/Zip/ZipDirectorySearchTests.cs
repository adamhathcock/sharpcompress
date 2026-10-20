using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipDirectorySearchTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(4096, false)]
    [InlineData(4096, true)]
    [InlineData(ushort.MaxValue, false)]
    [InlineData(ushort.MaxValue, true)]
    public async Task Empty_Archive_With_Comment_And_Short_Reads(int commentLength, bool async)
    {
        var bytes = new byte[22 + commentLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x06054b50);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), (ushort)commentLength);
        bytes.AsSpan(22).Fill((byte)'a');
        using var input = new ShortReadStream(new MemoryStream(bytes), maxReadSize: 7);
        if (async)
        {
            await using var archive = await ZipArchive.OpenAsyncArchive(
                input,
                ReaderOptions.ForExternalStream
            );
            Assert.Empty(await archive.EntriesAsync.ToListAsync());
        }
        else
        {
            using var archive = ZipArchive.OpenArchive(input, ReaderOptions.ForExternalStream);
            Assert.Empty(archive.Entries);
        }
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task Missing_Directory_With_Partial_Signature_Throws_ArchiveException(
        int prefixLength,
        bool async
    )
    {
        var bytes = new byte[31];
        var signature = new byte[] { 0x06, 0x05, 0x4b, 0x50 };
        for (var i = 0; i < prefixLength; i++)
        {
            bytes[prefixLength - i - 1] = signature[i];
        }
        using var input = new MemoryStream(bytes);
        var factory = new SeekableZipHeaderFactory(null, new ArchiveEncoding());
        if (async)
        {
            await Assert.ThrowsAsync<ArchiveException>(async () =>
                await factory.ReadSeekableHeaderAsync(input).ToListAsync()
            );
        }
        else
        {
            Assert.Throws<ArchiveException>(() => factory.ReadSeekableHeader(input).ToList());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Directory_Search_Rejects_Premature_End_Of_Stream(bool async)
    {
        using var input = new PrematureEndStream();
        var factory = new SeekableZipHeaderFactory(null, new ArchiveEncoding());
        if (async)
        {
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
                await factory.ReadSeekableHeaderAsync(input).ToListAsync()
            );
        }
        else
        {
            Assert.Throws<IncompleteArchiveException>(() =>
                factory.ReadSeekableHeader(input).ToList()
            );
        }
    }

    private sealed class PrematureEndStream : MemoryStream
    {
        public PrematureEndStream()
            : base(new byte[31]) { }

        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, (int)Math.Min(count, Math.Max(0, 10 - Position)));

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.FromResult(Read(buffer, offset, count));

#if !LEGACY_DOTNET
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        ) =>
            new(
                base.Read(
                    buffer.Span.Slice(0, (int)Math.Min(buffer.Length, Math.Max(0, 10 - Position)))
                )
            );
#endif
    }
}
