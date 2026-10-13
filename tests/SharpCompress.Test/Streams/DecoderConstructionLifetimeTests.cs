using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DecoderConstructionLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedDecoderReturnsPackCache(bool asyncOpen)
    {
        var folder = new CFolder();
        folder._coders.Add(
            new CCoderInfo
            {
                _methodId = new CMethodId(ulong.MaxValue),
                _numInStreams = 1,
                _numOutStreams = 1,
            }
        );
        folder._packStreams.Add(0);
        folder._unpackSizes.Add(1);
        using var input = new MemoryStream(new byte[1]);
        var pool = new CountingPool();
        if (asyncOpen)
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await DecoderStreamHelper.CreateDecoderStreamAsync(
                    input,
                    0,
                    new long[] { 1 },
                    folder,
                    new NoPassword(),
                    CancellationToken.None,
                    pool
                )
            );
        }
        else
        {
            Assert.Throws<NotSupportedException>(() =>
                DecoderStreamHelper.CreateDecoderStream(
                    input,
                    0,
                    new long[] { 1 },
                    folder,
                    new NoPassword(),
                    pool
                )
            );
        }
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(input.CanRead);
    }

    private sealed class CountingPool : ArrayPool<byte>
    {
        public int ReturnCount { get; private set; }

        public override byte[] Rent(int minimumLength) => new byte[minimumLength];

        public override void Return(byte[] array, bool clearArray = false) => ReturnCount++;
    }

    private sealed class NoPassword : IPasswordProvider
    {
        public string? CryptoGetTextPassword() => null;
    }
}
