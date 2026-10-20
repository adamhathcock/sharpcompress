using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DeflateReadAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bit_Length_Trees_Keep_Empty_And_Oversubscribed_Validation(bool oversubscribed)
    {
        var lengths = new int[19];
        if (oversubscribed)
        {
            lengths[0] = lengths[1] = lengths[2] = 1;
        }
        var depth = 7;
        var offset = 0;
        var codec = new ZlibCodec();
        var result = new InfTree().inflate_trees_bits(
            lengths,
            ref depth,
            ref offset,
            new int[8192],
            codec
        );
        Assert.Equal(ZlibConstants.Z_DATA_ERROR, result);
        Assert.Equal(
            oversubscribed
                ? "oversubscribed dynamic bit lengths tree"
                : "incomplete dynamic bit lengths tree",
            codec.Message
        );
        if (!oversubscribed)
        {
            Assert.Equal(0, depth);
            Assert.Equal(-1, offset);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fixed_And_Stored_Blocks_Keep_ReadAhead(bool useAsync)
    {
        // Non-final fixed empty block, then final stored block containing 1,2,3.
        byte[] packed = [0x02, 0x04, 0x03, 0, 0xFC, 0xFF, 1, 2, 3, 99, 98];
        using var input = new MemoryStream(packed);
        using var decoder = new SharpCompress.Compressors.Deflate.DeflateStream(
            input,
            CompressionMode.Decompress,
            CompressionLevel.Default,
            leaveOpen: true
        );
        var bytes = new byte[1];
        using var actual = new MemoryStream();
        int read;
        while (
            (
                read = useAsync
                    ? await decoder.ReadAsync(bytes, 0, 1, CancellationToken.None)
                    : decoder.Read(bytes, 0, 1)
            ) != 0
        )
        {
            actual.Write(bytes, 0, read);
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, actual.ToArray());
        using var remaining = decoder.InputBuffer;
        Assert.Equal(new byte[] { 99, 98 }, remaining.ToArray());
    }

#if !LEGACY_DOTNET
    [Fact]
    public async Task NonSeekable_Reader_Does_Not_Allocate_Compressed_Payload_Copies()
    {
        var expected = new byte[2 * 1024 * 1024];
        new Random(1234).NextBytes(expected);
        using var packed = new MemoryStream();
        using (
            var zip = new System.IO.Compression.ZipArchive(
                packed,
                System.IO.Compression.ZipArchiveMode.Create,
                leaveOpen: true
            )
        )
        {
            using var entry = zip.CreateEntry("random.bin").Open();
            entry.Write(expected);
        }
        packed.Position = 0;
        using var input = new ForwardOnlyStream(packed);
        await using var reader = await ReaderFactory.OpenAsyncReader(
            input,
            ReaderOptions.ForExternalStream,
            CancellationToken.None
        );
        Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
        await using var decoded = await reader.OpenEntryStreamAsync(CancellationToken.None);
        var buffer = new byte[32768];
        var total = await decoded.ReadAsync(buffer.AsMemory(), CancellationToken.None);
        Assert.True(expected.AsSpan(0, total).SequenceEqual(buffer.AsSpan(0, total)));
        var start = GC.GetAllocatedBytesForCurrentThread();
        int count;
        while ((count = await decoded.ReadAsync(buffer.AsMemory(), CancellationToken.None)) != 0)
        {
            Assert.True(expected.AsSpan(total, count).SequenceEqual(buffer.AsSpan(0, count)));
            total += count;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(expected.Length, total);
        // Memory-backed I/O completes synchronously, keeping this counter on one thread.
        // A temporary array per compressed refill would allocate about 2 MiB.
        Assert.InRange(allocated, 0, 131072);
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RingBuffered_Memory_Read_Preserves_Slices_Across_Replay_And_Fresh_Input(
        bool nonArray
    )
    {
        var expected = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        using var input = new ForwardOnlyStream(new MemoryStream(expected));
        using var stream = SharpCompressStream.Create(input, 16);
        var bytes = Enumerable.Repeat((byte)0xCC, 22).ToArray();
        using var owner = new NonArrayMemory(bytes);
        var memory = (nonArray ? owner.Memory : bytes.AsMemory()).Slice(5, 12);
        Assert.Equal(12, await stream.ReadAsync(memory, CancellationToken.None));
        Assert.Equal(12, await stream.ReadAsync(memory, CancellationToken.None));
        // The ring has wrapped. Combine six replayed bytes with six fresh bytes.
        stream.Position = 18;
        Assert.Equal(12, await stream.ReadAsync(memory, CancellationToken.None));
        Assert.Equal(expected.Skip(18).Take(12), bytes.Skip(5).Take(12));
        Assert.Equal(30, stream.Position);
        // Replay the fresh bytes too, verifying the source offset used when recording.
        stream.Position = 24;
        Assert.Equal(12, await stream.ReadAsync(memory, CancellationToken.None));
        Assert.Equal(expected.Skip(24).Take(12), bytes.Skip(5).Take(12));
        Assert.Equal(36, stream.Position);
        Assert.All(bytes.Take(5), b => Assert.Equal(0xCC, b));
        Assert.All(bytes.Skip(17), b => Assert.Equal(0xCC, b));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RingBuffer_Fresh_Input_Does_Not_Allocate_Per_Read(bool nonArray)
    {
        const int length = 1024 * 1024;
        using var input = new ForwardOnlyStream(new MemoryStream(new byte[length]));
        using var stream = SharpCompressStream.Create(input, 8192);
        var buffer = new byte[4106];
        using var owner = new NonArrayMemory(buffer);
        var memory = (nonArray ? owner.Memory : buffer.AsMemory()).Slice(5, 4096);
        var total = await stream.ReadAsync(memory, CancellationToken.None);
        var start = GC.GetAllocatedBytesForCurrentThread();
        int count;
        while ((count = await stream.ReadAsync(memory, CancellationToken.None)) != 0)
        {
            total += count;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(length, total);
        Assert.InRange(allocated, 0, 65536);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RingBuffer_Replay_Does_Not_Allocate_Per_Read(bool nonArray)
    {
        using var input = new ForwardOnlyStream(new MemoryStream(new byte[32]));
        using var stream = SharpCompressStream.Create(input, 16);
        var buffer = new byte[22];
        using var owner = new NonArrayMemory(buffer);
        var memory = (nonArray ? owner.Memory : buffer.AsMemory()).Slice(5, 12);
        await stream.ReadAsync(memory, CancellationToken.None);
        stream.Position = 0;
        await stream.ReadAsync(memory, CancellationToken.None);
        var start = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var i = 0; i < 10000; i++)
        {
            stream.Position = 0;
            total += await stream.ReadAsync(memory, CancellationToken.None);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(120000, total);
        Assert.InRange(allocated, 0, 65536);
    }

    [Fact]
    public async Task NonArray_Fallback_Reuses_Its_Rental_Across_Many_Reads()
    {
        const int length = 1024 * 1024;
        using var input = new MemoryInput(Compress(new byte[length]), false);
        using var decoder = CreateDecoder(input);
        using var owner = new NonArrayMemory(new byte[64]);
        var memory = owner.Memory;
        var total = await decoder.ReadAsync(memory, CancellationToken.None);
        var start = GC.GetAllocatedBytesForCurrentThread();
        int count;
        while ((count = await decoder.ReadAsync(memory, CancellationToken.None)) != 0)
        {
            total += count;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(length, total);
        // A lost 64-byte rental per read would allocate over a megabyte here.
        // Leave ample room for incidental initialization rather than asserting zero.
        Assert.InRange(allocated, 0, 65536);
        Assert.Null(decoder._z.OutputBuffer);
    }

    [Fact]
    public void Generated_Sync_Read_Uses_The_Existing_Array_Overload()
    {
        var expected = new byte[4096];
        new Random(1234).NextBytes(expected);
        using var source = new ArrayOnlyInput(Compress(expected));
        using var decoder = CreateDecoder(source);
        using var output = new MemoryStream();
        decoder.CopyTo(output, 8192);
        Assert.Equal(expected, output.ToArray());
        Assert.True(source.ArrayReads > 0);
        Assert.Null(decoder._z.OutputBuffer);
    }

    private sealed class ArrayOnlyInput(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public int ArrayReads { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArrayReads++;
            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer) =>
            throw new NotSupportedException("Sync reads must retain array semantics.");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Memory_Read_Preserves_Slices_And_Releases_Output(bool nonArray, bool suspend)
    {
        var expected = new byte[32769];
        new Random(4321).NextBytes(expected);
        using var input = new MemoryInput(Compress(expected), suspend);
        using var decoder = CreateDecoder(input);
        var bytes = Enumerable.Repeat((byte)0xCC, 79).ToArray();
        using var owner = new NonArrayMemory(bytes);
        var destination = nonArray ? owner.Memory : bytes.AsMemory();
        input.BeforeRead = () =>
        {
            Assert.NotNull(decoder._z.OutputBuffer);
            if (!nonArray)
            {
                Assert.Same(bytes, decoder._z.OutputBuffer);
            }
            else
            {
                Assert.NotSame(bytes, decoder._z.OutputBuffer);
            }
        };
        using var actual = new MemoryStream();
        while (true)
        {
            var count = await decoder.ReadAsync(destination.Slice(7, 63), CancellationToken.None);
            Assert.Null(decoder._z.OutputBuffer);
            Assert.All(bytes.Take(7), b => Assert.Equal(0xCC, b));
            Assert.All(bytes.Skip(70), b => Assert.Equal(0xCC, b));
            if (count == 0)
            {
                break;
            }
            actual.Write(bytes, 7, count);
            // No caller-memory reference may survive until the next operation.
            Array.Fill(bytes, (byte)0xCC);
        }
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(0, await decoder.ReadAsync(Memory<byte>.Empty, CancellationToken.None));
        Assert.Null(decoder._z.OutputBuffer);
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Memory_Read_Cancellation_And_Failure_Release_Output(bool nonArray)
    {
        var bytes = new byte[128];
        using var owner = new NonArrayMemory(bytes);
        var memory = nonArray ? owner.Memory : bytes.AsMemory();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using (var input = new MemoryInput(Compress(bytes), false))
        using (var decoder = CreateDecoder(input))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await decoder.ReadAsync(memory, cancelled.Token)
            );
            Assert.Null(decoder._z.OutputBuffer);
        }
        using (var input = new MemoryInput(Compress(bytes), true) { Fail = true })
        using (var decoder = CreateDecoder(input))
        {
            await Assert.ThrowsAsync<IOException>(async () =>
                await decoder.ReadAsync(memory, CancellationToken.None)
            );
            Assert.Null(decoder._z.OutputBuffer);
        }
        using (var input = new MemoryInput(new byte[] { 7 }, false))
        using (var decoder = CreateDecoder(input))
        {
            await Assert.ThrowsAsync<ZlibException>(async () =>
                await decoder.ReadAsync(memory, CancellationToken.None)
            );
            Assert.Null(decoder._z.OutputBuffer);
        }
    }

    private static ZlibBaseStream CreateDecoder(Stream input) =>
        new(
            input,
            CompressionMode.Decompress,
            CompressionLevel.Default,
            ZlibStreamFlavor.DEFLATE,
            leaveOpen: true,
            Encoding.UTF8
        );

    private sealed class MemoryInput(byte[] bytes, bool suspend) : Stream
    {
        private int position;
        private bool disposed;
        public Action? BeforeRead { get; set; }
        public bool Fail { get; set; }
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException("Only memory reads are supported.");

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeforeRead?.Invoke();
            if (suspend)
            {
                await Task.Yield();
            }
            if (Fail)
            {
                throw new IOException("Input failed.");
            }
            var count = Math.Min(Math.Min(buffer.Length, 17), bytes.Length - position);
            bytes.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return count;
        }

        public override void Flush() { }

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

    private sealed class NonArrayMemory(byte[] bytes) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => bytes;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
#endif

    private static byte[] Compress(byte[] bytes)
    {
        using var packed = new MemoryStream();
        using (
            var encoder = new System.IO.Compression.DeflateStream(
                packed,
                System.IO.Compression.CompressionLevel.Optimal,
                leaveOpen: true
            )
        )
        {
            encoder.Write(bytes, 0, bytes.Length);
        }
        return packed.ToArray();
    }
}
