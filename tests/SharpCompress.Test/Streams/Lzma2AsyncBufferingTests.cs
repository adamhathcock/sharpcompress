#if !LEGACY_DOTNET
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.Streams;

public class Lzma2AsyncBufferingTests
{
    // liblzma raw LZMA2, 4 KiB dictionary, one compressed chunk followed by its end marker.
    private static byte[] Packed() =>
        Convert.FromBase64String(
            "4Ii3AGldADEbyxGbBQY80wYkJNZUZojJCx/dH+R7NYxybfjsA6YFO6vj+t6Pjqr8q+3nmIKYLQdQf3feK0ZBZu3t1pE+S0M9VTCcsH3/PD9kncWqHcTIHqGAu441fiPlqtCCWEf8CABLZsj8KdrAAAA="
        );

    private static byte[] Expected() =>
        Encoding.ASCII.GetBytes(
            string.Concat(Enumerable.Repeat("bounded asynchronous LZMA2 payload\n", 1000))
        );

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 7)]
    [InlineData(true, 7)]
    [InlineData(false, 65536)]
    [InlineData(true, 65536)]
    public async Task UsesOnlyAsyncInputAndStopsAtChunkBoundary(bool memory, int maxRead)
    {
        var packed = Packed();
        var expected = Expected();
        using var input = new RecordingInput(
            packed.Concat(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }).ToArray(),
            maxRead
        );
        await using var decoder = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            packed.Length,
            expected.Length,
            leaveOpen: true
        );
        using var actual = new MemoryStream();
        var buffer = new byte[17000];
        int read;
        while (
            (
                read = memory
                    ? await decoder.ReadAsync(buffer.AsMemory(), CancellationToken.None)
                    : await decoder.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
            ) != 0
        )
        {
            actual.Write(buffer, 0, read);
        }
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(packed.Length, input.BytesRead);
        Assert.InRange(input.MaxRequested, 5, 65536);
        if (maxRead == 65536)
        {
            // Per-byte range decoding would issue more than 100 source reads here.
            Assert.InRange(input.ReadCalls, 1, 12);
        }
        await decoder.DisposeAsync();
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidShortPayloadNeverReadsFollowingChunk(bool memory)
    {
        // The declared four-byte compressed payload is too short for range initialization.
        var bytes = new byte[] { 0xE0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0xDE, 0xAD };
        using var input = new RecordingInput(bytes, 65536);
        await using var decoder = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        var buffer = new byte[1];
        await Assert.ThrowsAsync<DataErrorException>(async () =>
        {
            if (memory)
            {
                await decoder.ReadAsync(buffer.AsMemory(), CancellationToken.None);
            }
            else
            {
                await decoder.ReadAsync(buffer, 0, 1, CancellationToken.None);
            }
        });
        Assert.Equal(6, input.BytesRead);
        // Retrying after a failed fill must not enter unsafe decoding with an uninitialized buffer.
        await Assert.ThrowsAsync<DataErrorException>(() =>
            decoder.ReadAsync(buffer, 0, 1, CancellationToken.None)
        );
        Assert.Equal(6, input.BytesRead);
    }

    [Fact]
    public async Task FailedPayloadFillReturnsInputLeaseOnDisposal()
    {
        var packed = Packed();
        using var cancellation = new CancellationTokenSource();
        using var input = new RecordingInput(packed, 7);
        input.AfterRead = position =>
        {
            if (position > 6)
            {
                cancellation.Cancel();
            }
        };
        var decoder = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decoder.ReadAsync(new byte[1], 0, 1, cancellation.Token)
        );
        var range = GetField(decoder, "_rangeDecoder")!;
        Assert.NotNull(GetField(range, "_fastBuffer"));
        await decoder.DisposeAsync();
        decoder.Dispose();
        Assert.Null(GetField(range, "_fastBuffer"));
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task TruncatedPayloadCannotRetryUnsafeDecodingAndReturnsLease()
    {
        using var input = new RecordingInput(Packed().Take(20).ToArray(), 7);
        var decoder = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        var buffer = new byte[1];
        await Assert.ThrowsAsync<DataErrorException>(() =>
            decoder.ReadAsync(buffer, 0, 1, CancellationToken.None)
        );
        var range = GetField(decoder, "_rangeDecoder")!;
        Assert.NotNull(GetField(range, "_fastBuffer"));
        await Assert.ThrowsAsync<DataErrorException>(() =>
            decoder.ReadAsync(buffer, 0, 1, CancellationToken.None)
        );
        Assert.Equal(20, input.BytesRead);
        await decoder.DisposeAsync();
        Assert.Null(GetField(range, "_fastBuffer"));
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task CancellationAfterPayloadBufferingCanResumeWithoutLosingData()
    {
        var packed = Packed();
        var payloadEnd = 6 + ((packed[3] << 8) | packed[4]) + 1;
        using var cancellation = new CancellationTokenSource();
        using var input = new RecordingInput(packed, 65536);
        input.AfterRead = position =>
        {
            if (position == payloadEnd)
            {
                cancellation.Cancel();
            }
        };
        await using var decoder = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decoder.ReadAsync(new byte[1], 0, 1, cancellation.Token)
        );
        input.AfterRead = null;
        using var output = new MemoryStream();
        await decoder.CopyToAsync(output, 65536, CancellationToken.None);
        Assert.Equal(Expected(), output.ToArray());
        Assert.Equal(packed.Length, input.BytesRead);
    }

    [Fact]
    public async Task OversizedPoisonedRentUsesOnlyDeclaredPayloadBytes()
    {
        var packed = Packed();
        using var input = new RecordingInput(packed, 7);
        await using var decoder = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        var range = GetField(decoder, "_rangeDecoder")!;
        var oversized = ArrayPool<byte>.Shared.Rent(128 * 1024);
        Array.Fill(oversized, (byte)0xCC);
        range
            .GetType()
            .GetField("_fastBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(range, oversized);
        var first = new byte[1];
        Assert.Equal(1, await decoder.ReadAsync(first.AsMemory(), CancellationToken.None));
        Assert.Same(oversized, GetField(range, "_fastBuffer"));
        Assert.Equal(((packed[3] << 8) | packed[4]) + 1, GetField(range, "_fastBufferLen"));
        using var output = new MemoryStream();
        output.Write(first);
        await decoder.CopyToAsync(output, 65536, CancellationToken.None);
        Assert.Equal(Expected(), output.ToArray());
        Assert.Null(GetField(range, "_fastBuffer"));
    }

    private static object? GetField(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private sealed class RecordingInput(byte[] bytes, int maxRead) : Stream
    {
        private bool disposed;
        public int BytesRead { get; private set; }
        public int MaxRequested { get; private set; }
        public int ReadCalls { get; private set; }
        public Action<int>? AfterRead { get; set; }
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("No synchronous I/O");

        public override int ReadByte() => throw new NotSupportedException("No synchronous I/O");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            MaxRequested = Math.Max(MaxRequested, buffer.Length);
            ReadCalls++;
            var count = Math.Min(Math.Min(buffer.Length, maxRead), bytes.Length - BytesRead);
            bytes.AsMemory(BytesRead, count).CopyTo(buffer);
            BytesRead += count;
            AfterRead?.Invoke(BytesRead);
            return count;
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            disposed = true;
            base.Dispose(disposing);
        }
    }
}
#endif
