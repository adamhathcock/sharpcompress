using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Crypto;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers.SevenZip;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzmaModelReuseTests
{
    [Theory]
    [InlineData(CompressionType.LZMA, false)]
    [InlineData(CompressionType.LZMA, true)]
    [InlineData(CompressionType.LZMA2, false)]
    [InlineData(CompressionType.LZMA2, true)]
    public async Task Writer_Pooled_Copy_Handles_Buffer_Boundaries_And_Crc(
        CompressionType compressionType,
        bool useAsync
    )
    {
        var expected = new byte[131089];
        new Random(789).NextBytes(expected);
        using var packed = new MemoryStream();
        var options = new SevenZipWriterOptions(compressionType) { LeaveStreamOpen = true };
        using var source = new MemoryStream(expected, writable: false);
        if (useAsync)
        {
            await using var writer = new SevenZipWriter(packed, options);
            await writer.WriteAsync("payload", source, null, CancellationToken.None);
        }
        else
        {
            using var writer = new SevenZipWriter(packed, options);
            writer.Write("payload", source, null);
        }
        packed.Position = 0;
        using var actual = new MemoryStream();
        if (useAsync)
        {
            await using var archive = await SevenZipArchive.OpenAsyncArchive(
                packed,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            await foreach (var entry in archive.EntriesAsync)
            {
                var input = await entry.OpenEntryStreamAsync(CancellationToken.None);
                await using var scope = input.DisposeAsyncScope();
                await input.CopyToAsync(actual, 65536, CancellationToken.None);
                Assert.Equal(unchecked((uint)entry.Crc), Crc32Stream.Compute(actual.ToArray()));
            }
        }
        else
        {
            using var archive = SevenZipArchive.OpenArchive(
                packed,
                ReaderOptions.ForExternalStream
            );
            foreach (var entry in archive.Entries)
            {
                using var input = entry.OpenEntryStream();
                input.CopyTo(actual);
                Assert.Equal(unchecked((uint)entry.Crc), Crc32Stream.Compute(actual.ToArray()));
            }
        }
        Assert.Equal(expected, actual.ToArray());
        Assert.True(packed.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma2_Resets_Reuse_Decoder_And_Reinitialize_Changed_Properties(bool useAsync)
    {
        var payload = new byte[4096];
        new Random(901).NextBytes(payload);
        payload[payload.Length - 1] = 0;
        using var packed = new MemoryStream();
        // Every boundary has an aligned position and a zero previous byte, as
        // expected by the fresh raw encoders used to build these independent chunks.
        WriteChunk(packed, payload, 0xE0, 3, 0, 2);
        WriteChunk(packed, payload, 0xC0, 2, 1, 1);
        WriteChunk(packed, payload, 0xA0, 2, 1, 1);
        WriteChunk(packed, payload, 0xE0, 1, 0, 0);
        packed.WriteByte(0);
        packed.Position = 0;
        using var decoder = LzmaStream.Create(
            new byte[] { 16 },
            packed,
            packed.Length,
            payload.Length * 4,
            leaveOpen: true
        );
        object? previous = null;
        var actual = new byte[payload.Length];
        for (var chunk = 0; chunk < 4; chunk++)
        {
            var offset = 0;
            while (offset < actual.Length)
            {
                var read = useAsync
                    ? await decoder.ReadAsync(
                        actual,
                        offset,
                        actual.Length - offset,
                        CancellationToken.None
                    )
                    : decoder.Read(actual, offset, actual.Length - offset);
                Assert.True(read > 0);
                offset += read;
            }
            Assert.Equal(payload, actual);
            var current = GetField(decoder, "_decoder")!;
            if (previous is not null)
            {
                Assert.Same(previous, current);
            }
            previous = current;
#if !LEGACY_DOTNET
            if (useAsync)
            {
                Assert.Null(GetField(current, "_fLiteral"));
            }
            else
            {
                Assert.Null(GetField(GetField(current, "_literalDecoder")!, "_models"));
            }
#endif
        }
        var end = useAsync
            ? await decoder.ReadAsync(actual, 0, 1, CancellationToken.None)
            : decoder.Read(actual, 0, 1);
        Assert.Equal(0, end);
        Assert.True(packed.CanRead);
    }

    [Fact]
    public void Small_History_Avoids_Excess_Growth_And_Returns_All_Buffers()
    {
        var pool = new TrackingPool();
        using var window = new OutWindow(pool);
        window.Create(1 << 20);
        Assert.Equal(4096, window.BufferCapacity);
        const int length = 70 * 1024;
        window.SetLimit(length);
        for (var i = 0; i < length; i++)
        {
            window.PutByte((byte)i);
        }
        Assert.Equal(new[] { 4096, 8192, 16384, 32768, 65536, 131072 }, pool.Requests);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal((byte)(length - 1 - i), window.GetByte(i));
        }
        window.Dispose();
        window.Dispose();
        Assert.Equal(pool.Requests.Count, pool.ReturnCount);
    }

#if !LEGACY_DOTNET
    [Fact]
    public async Task Async_Copy_And_Cache_Refill_Use_Memory_Reads()
    {
        var expected = new byte[8192];
        new Random(456).NextBytes(expected);
        using (var source = new MemoryOnlySource(expected))
        using (var window = new OutWindow())
        {
            window.Create(16384);
            window.SetLimit(expected.Length);
            Assert.Equal(
                expected.Length,
                await window.CopyStreamAsync(source, expected.Length, CancellationToken.None)
            );
            var actual = new byte[expected.Length];
            Assert.Equal(actual.Length, window.Read(actual, 0, actual.Length));
            Assert.Equal(expected, actual);
        }
        using (var source = new MemoryOnlySource(expected))
        using (
            var cache = new BufferedSubStream(source, 0, expected.Length, ArrayPool<byte>.Shared)
        )
        {
            var actual = new byte[expected.Length];
            Assert.Equal(
                actual.Length,
                await cache.ReadAsync(actual.AsMemory(), CancellationToken.None)
            );
            Assert.Equal(expected, actual);
        }
    }

    private sealed class MemoryOnlySource(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException("The Task-returning overload must not be used.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(base.Read(buffer.Span));
        }
    }
#endif

    private static void WriteChunk(
        Stream output,
        byte[] payload,
        int control,
        int lc,
        int lp,
        int pb
    )
    {
        using var encoder = new Encoder();
        encoder.SetCoderProperties(
            new[]
            {
                CoderPropId.DictionarySize,
                CoderPropId.LitContextBits,
                CoderPropId.LitPosBits,
                CoderPropId.PosStateBits,
                CoderPropId.EndMarker,
            },
            new object[] { 1 << 20, lc, lp, pb, false }
        );
        using var packed = new MemoryStream();
        using var source = new MemoryStream(payload, writable: false);
        encoder.Code(source, packed, payload.Length, -1, null!);
        var data = packed.ToArray();
        output.WriteByte((byte)control);
        output.WriteByte((byte)((payload.Length - 1) >> 8));
        output.WriteByte((byte)(payload.Length - 1));
        output.WriteByte((byte)((data.Length - 1) >> 8));
        output.WriteByte((byte)(data.Length - 1));
        if (control >= 0xC0)
        {
            output.WriteByte((byte)((pb * 5 + lp) * 9 + lc));
        }
        output.Write(data, 0, data.Length);
    }

    private static object? GetField(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private sealed class TrackingPool : ArrayPool<byte>
    {
        public List<int> Requests { get; } = new();
        public int ReturnCount { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            Requests.Add(minimumLength);
            return new byte[minimumLength];
        }

        public override void Return(byte[] array, bool clearArray = false) => ReturnCount++;
    }
}
