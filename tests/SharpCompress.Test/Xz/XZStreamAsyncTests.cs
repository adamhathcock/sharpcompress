using System.IO;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzStreamAsyncTests : XzTestsBase
{
    [Fact]
    public async ValueTask CanReadEmptyStreamAsync()
    {
        var xz = new XZStream(CompressedEmptyStream);
        using var sr = new StreamReader(xz);
        var uncompressed = await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(OriginalEmpty, uncompressed);
    }

    [Fact]
    public async ValueTask CanReadStreamAsync()
    {
        var xz = new XZStream(CompressedStream);
        using var sr = new StreamReader(xz);
        var uncompressed = await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(Original, uncompressed);
    }

    [Fact]
    public async ValueTask CanReadIndexedStreamAsync()
    {
        var xz = new XZStream(CompressedIndexedStream);
        using var sr = new StreamReader(xz);
        var uncompressed = await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(OriginalIndexed, uncompressed);
    }
}
