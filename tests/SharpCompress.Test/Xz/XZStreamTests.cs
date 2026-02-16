using System.IO;
using SharpCompress.Compressors.Xz;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzStreamTests : XzTestsBase
{
    [Fact]
    public void CanReadEmptyStream()
    {
        var xz = new XZStream(CompressedEmptyStream);
        using var sr = new StreamReader(xz);
        var uncompressed = sr.ReadToEnd();
        Assert.Equal(OriginalEmpty, uncompressed);
    }

    [Fact]
    public void CanReadStream()
    {
        var xz = new XZStream(CompressedStream);
        using var sr = new StreamReader(xz);
        var uncompressed = sr.ReadToEnd();
        Assert.Equal(Original, uncompressed);
    }

    [Fact]
    public void CanReadIndexedStream()
    {
        var xz = new XZStream(CompressedIndexedStream);
        using var sr = new StreamReader(xz);
        var uncompressed = sr.ReadToEnd();
        Assert.Equal(OriginalIndexed, uncompressed);
    }

    [Fact]
    public void CanReadNonSeekableStream()
    {
        var nonSeekable = new ForwardOnlyStream(new MemoryStream(Compressed));
        var xz = new XZStream(SharpCompressStream.Create(nonSeekable));
        using var sr = new StreamReader(xz);
        var uncompressed = sr.ReadToEnd();
        Assert.Equal(Original, uncompressed);
    }

    [Fact]
    public void CanReadNonSeekableEmptyStream()
    {
        var nonSeekable = new ForwardOnlyStream(new MemoryStream(CompressedEmpty));
        var xz = new XZStream(SharpCompressStream.Create(nonSeekable));
        using var sr = new StreamReader(xz);
        var uncompressed = sr.ReadToEnd();
        Assert.Equal(OriginalEmpty, uncompressed);
    }
}
