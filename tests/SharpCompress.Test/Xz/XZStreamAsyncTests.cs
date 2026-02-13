using System.IO;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzStreamAsyncTests : XzTestsBase
{
    [Fact]
    public async ValueTask CanReadEmptyStreamAsync()
    {
        using var xz = new XZStream(CompressedEmptyStream);
        using var sr = new StreamReader(new AsyncOnlyStream(xz));
        var uncompressed = await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(OriginalEmpty, uncompressed);
    }

    [Fact]
    public async ValueTask CanReadStreamAsync()
    {
        using var xz = new XZStream(CompressedStream);
        using var sr = new StreamReader(new AsyncOnlyStream(xz));
        var uncompressed = await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(Original, uncompressed);
    }

    [Fact]
    public async ValueTask CanReadIndexedStreamAsync()
    {
        using var xz = new XZStream(CompressedIndexedStream);
        using var sr = new StreamReader(new AsyncOnlyStream(xz));
        var uncompressed = await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(OriginalIndexed, uncompressed);
    }
}
