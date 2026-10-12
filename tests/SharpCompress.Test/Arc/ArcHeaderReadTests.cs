using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arc;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Arc;

public class ArcHeaderReadTests
{
    [Fact]
    public async Task Arc_Header_Allows_Partial_Reads()
    {
        var header = new byte[29];
        header[0] = 0x1A;
        header[1] = 2;
        header[2] = (byte)'x';
        using var sync = new ShortReadStream(new MemoryStream(header));
        using var asyncInput = new ShortReadStream(new MemoryStream(header));
        var parser = new ArcEntryHeader(new ArchiveEncoding());
        Assert.Equal("x", parser.ReadHeader(sync)!.Name);
        Assert.Equal("x", (await parser.ReadHeaderAsync(asyncInput, CancellationToken.None))!.Name);
    }
}
