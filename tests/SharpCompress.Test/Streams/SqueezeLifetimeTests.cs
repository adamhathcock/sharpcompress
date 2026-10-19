using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.PPMd;
using SharpCompress.Compressors.Squeezed;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SqueezeLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeReleasesOwnedHuffmanStream(bool asyncOpen)
    {
        // One Huffman node: literal A on zero, end marker on one.
        using var source = new MemoryStream(new byte[] { 1, 0, 190, 255, 255, 254, 2 });
        var stream = asyncOpen
            ? await SqueezeStream.CreateAsync(source, 7, CancellationToken.None)
            : SqueezeStream.Create(source, 7);
        var owned = (Stream)
            typeof(SqueezeStream)
                .GetField("_huffmanDecoded", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(stream)!;
        Assert.True(owned.CanRead);
        stream.Dispose();
        Assert.False(owned.CanRead);
        Assert.True(source.CanRead);
    }

    [Fact]
    public void UnsupportedPpmdCompressionLeavesInputOpen()
    {
        using var source = new MemoryStream();
        var properties = new PpmdProperties(new byte[] { 2, 0, 0, 16, 0 });
        Assert.Throws<NotImplementedException>(() => PpmdStream.Create(properties, source, true));
        Assert.True(source.CanRead);
    }
}
