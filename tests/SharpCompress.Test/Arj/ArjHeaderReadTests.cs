using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Crypto;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Arj;

public class ArjHeaderReadTests
{
    [Fact]
    public async Task Arj_Header_Allows_Partial_Reads()
    {
        byte[] header = [0x60, 0xEA, 1, 0, 42, 0, 0, 0, 0];
        BitConverter.GetBytes(Crc32Stream.Compute(new byte[] { 42 })).CopyTo(header, 5);
        var parser = new ArjMainHeader(new ArchiveEncoding());
        using var sync = new ShortReadStream(new MemoryStream(header));
        using var asyncInput = new ShortReadStream(new MemoryStream(header));
        Assert.Equal(new byte[] { 42 }, parser.ReadHeader(sync));
        Assert.Equal(
            new byte[] { 42 },
            await parser.ReadHeaderAsync(asyncInput, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Arj_Extended_Header_Allows_Partial_Reads()
    {
        byte[] header = [1, 0, 42, 0, 0, 0, 0, 0, 0];
        BitConverter.GetBytes(Crc32Stream.Compute(new byte[] { 42 })).CopyTo(header, 3);
        using var sync = new ShortReadStream(new MemoryStream(header));
        using var asyncInput = new ShortReadStream(new MemoryStream(header));
        var parser = new ExtendedArjHeader();
        Assert.Equal(new byte[] { 42 }, Assert.Single(parser.ReadExtended(sync)));
        Assert.Equal(new byte[] { 42 }, Assert.Single(await parser.ReadExtendedAsync(asyncInput)));
    }

    private sealed class ExtendedArjHeader : ArjMainHeader
    {
        public ExtendedArjHeader()
            : base(new ArchiveEncoding()) { }

        public List<byte[]> ReadExtended(Stream input) => ReadExtendedHeaders(input);

        public ValueTask<List<byte[]>> ReadExtendedAsync(Stream input) =>
            ReadExtendedHeadersAsync(input, CancellationToken.None);
    }
}
