using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Providers.Default;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

public class DeflateProviderBufferTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, 9)]
    [InlineData(true, 9)]
    public async Task Compression_Buffer_Preserves_Data_And_Flush(bool useAsync, int level)
    {
        var data = new byte[131073];
        new Random(1234).NextBytes(data);
        Array.Clear(data, 16384, 32768);
        var provider = new DeflateCompressionProvider();
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var stream = Assert.IsType<DeflateStream>(
                await provider.CreateCompressStreamAsync(
                    new AsyncOnlyStream(new ForwardOnlyStream(output)),
                    level,
                    CancellationToken.None
                )
            );
            Assert.Equal(65536, stream.BufferSize);
            stream.FlushMode = FlushType.Sync;
            await stream.WriteAsync(data, 0, 65537, CancellationToken.None);
            await stream.FlushAsync(CancellationToken.None);
            AssertFlushedPrefix(output.ToArray(), data, 65537);
            await stream.WriteAsync(data, 65537, data.Length - 65537, CancellationToken.None);
        }
        else
        {
            using var stream = Assert.IsType<DeflateStream>(
                provider.CreateCompressStream(new ForwardOnlyStream(output), level)
            );
            Assert.Equal(65536, stream.BufferSize);
            stream.FlushMode = FlushType.Sync;
            stream.Write(data, 0, 65537);
            stream.Flush();
            AssertFlushedPrefix(output.ToArray(), data, 65537);
            stream.Write(data, 65537, data.Length - 65537);
        }
        using var input = new MemoryStream(output.ToArray());
        using var decoder = Assert.IsType<DeflateStream>(provider.CreateDecompressStream(input));
        Assert.Equal(16384, decoder.BufferSize);
        using var extracted = new MemoryStream();
        decoder.CopyTo(extracted);
        Assert.Equal(data, extracted.ToArray());
#if !LEGACY_DOTNET
        using var nativeInput = new MemoryStream(output.ToArray());
        using var native = new System.IO.Compression.DeflateStream(
            nativeInput,
            System.IO.Compression.CompressionMode.Decompress
        );
        using var nativeOutput = new MemoryStream();
        native.CopyTo(nativeOutput);
        Assert.Equal(data, nativeOutput.ToArray());
#endif
    }

    private static void AssertFlushedPrefix(byte[] compressed, byte[] data, int length)
    {
        // Buffer sizes may change legal block layout at Sync flush boundaries.
        // Check that the entire prefix is available before finalization instead
        // of requiring byte-identical compressed representations.
        using var input = new MemoryStream(compressed);
        using var decoder = new DeflateStream(input, CompressionMode.Decompress);
        var prefix = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = decoder.Read(prefix, offset, length - offset);
            Assert.True(read > 0);
            offset += read;
        }
        Assert.Equal(data.AsSpan(0, length).ToArray(), prefix);
    }

    [Fact]
    public async Task Async_Creation_Observes_Cancellation()
    {
        var provider = new DeflateCompressionProvider();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await provider.CreateCompressStreamAsync(Stream.Null, 6, cancelled.Token)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_Compression_Closes_Owned_Output(bool useAsync)
    {
        var provider = new DeflateCompressionProvider();
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var stream = Assert.IsType<DeflateStream>(
                await provider.CreateCompressStreamAsync(
                    new AsyncOnlyStream(output),
                    6,
                    CancellationToken.None
                )
            );
        }
        else
        {
            using var stream = provider.CreateCompressStream(output, 6);
        }
        Assert.False(output.CanWrite);
        using var baseline = new MemoryStream();
        using (var stream = new DeflateStream(baseline, CompressionMode.Compress)) { }
        Assert.Equal(baseline.ToArray(), output.ToArray());
    }
}
