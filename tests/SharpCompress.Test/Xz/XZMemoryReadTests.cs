#if !LEGACY_DOTNET
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzMemoryReadTests
{
    // liblzma-generated x86+LZMA2 streams containing 2,000 repetitions of the expected line.
    public static TheoryData<string> Checks =>
        new()
        {
            "/Td6WFoAAAD/EtlBAgEEACEBAADaM60D4LOvAGBdACaZSach4xxvjplweW8sBThpghKnl3v62Kohix9VuFZ6Y6UNYeuBVsU4MUwSbr1yebmoUdjYLCUnhap0oivonKNF3AVwgPWqZ6UcPnRRZ9QNV+m11evb/jb/zCgx+5gpAAAAAXSw5wIAAPE+DQuoAAr8AgAAAAAAWVo=",
            "/Td6WFoAAAFpIt42AgEEACEBAADaM60D4LOvAGBdACaZSach4xxvjplweW8sBThpghKnl3v62Kohix9VuFZ6Y6UNYeuBVsU4MUwSbr1yebmoUdjYLCUnhap0oivonKNF3AVwgPWqZ6UcPnRRZ9QNV+m11evb/jb/zCgx+5gpAAAuAAwEAAF4sOcCAACK/s98PjANiwIAAAAAAVla",
            "/Td6WFoAAATm1rRGAgEEACEBAADaM60D4LOvAGBdACaZSach4xxvjplweW8sBThpghKnl3v62Kohix9VuFZ6Y6UNYeuBVsU4MUwSbr1yebmoUdjYLCUnhap0oivonKNF3AVwgPWqZ6UcPnRRZ9QNV+m11evb/jb/zCgx+5gpAADdy3Bd9bawwgABfLDnAgAAnLxe57HEZ/sCAAAAAARZWg==",
            "/Td6WFoAAArh+wyhAgEEACEBAADaM60D4LOvAGBdACaZSach4xxvjplweW8sBThpghKnl3v62Kohix9VuFZ6Y6UNYeuBVsU4MUwSbr1yebmoUdjYLCUnhap0oivonKNF3AVwgPWqZ6UcPnRRZ9QNV+m11evb/jb/zCgx+5gpAABZo8OamypsYGhitd9QtvP1Bfo7M36ti3jv0rEHlcfmJAABlAGw5wIABUMr+7bp3xwCAAAAAApZWg==",
        };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var check in Checks)
        {
            foreach (var nonArray in new[] { false, true })
            {
                foreach (var forwardOnly in new[] { false, true })
                {
                    foreach (var suspend in new[] { false, true })
                    {
                        yield return new object[] { check.Data, nonArray, forwardOnly, suspend };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MemoryReadsValidateAllChecksAndMemoryKinds(
        string compressed,
        bool nonArray,
        bool forwardOnly,
        bool suspend
    )
    {
        using var input = suspend
            ? new YieldingInput(Convert.FromBase64String(compressed))
            : new MemoryStream(Convert.FromBase64String(compressed));
        using var asyncOnly = new AsyncOnlyStream(input, disposeStream: false);
        using var forward = new ForwardOnlyStream(asyncOnly);
        using var tracked = SharpCompressStream.Create(forward);
        using var xz = new XZStream(forwardOnly ? tracked : asyncOnly);
        var bytes = new byte[10013];
        using var manager = new NonArrayMemory(bytes);
        var memory = (nonArray ? manager.Memory : bytes.AsMemory()).Slice(5, 10000);
        using var output = new MemoryStream();
        while (true)
        {
            Array.Fill(bytes, (byte)0xCC);
            var read = await xz.ReadAsync(memory, CancellationToken.None);
            Assert.True(bytes.AsSpan(0, 5).IndexOfAnyExcept((byte)0xCC) < 0);
            Assert.True(bytes.AsSpan(5 + read).IndexOfAnyExcept((byte)0xCC) < 0);
            if (read == 0)
            {
                break;
            }
            output.Write(bytes, 5, read);
        }
        Assert.Equal(Expected(), output.ToArray());
        xz.Dispose();
        Assert.True(input.CanRead);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await xz.ReadAsync(memory));
    }

    [Fact]
    public async Task MemoryAndArrayReadsCanBeInterleaved()
    {
        using var input = new MemoryStream(Convert.FromBase64String(Checks.Last().Data));
        using var xz = new XZStream(input);
        var buffer = new byte[17];
        using var output = new MemoryStream();
        for (var iteration = 0; ; iteration++)
        {
            var read =
                iteration % 2 == 0
                    ? await xz.ReadAsync(buffer.AsMemory(), CancellationToken.None)
                    : await xz.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
            if (read == 0)
            {
                break;
            }
            output.Write(buffer, 0, read);
        }
        Assert.Equal(Expected(), output.ToArray());
    }

    [Fact]
    public async Task EmptyAndCanceledMemoryReadsDoNotConsumeInput()
    {
        using var input = new MemoryStream(Convert.FromBase64String(Checks.First().Data));
        using var xz = new XZStream(input);
        Assert.Equal(0, await xz.ReadAsync(Memory<byte>.Empty));
        Assert.Equal(0, input.Position);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await xz.ReadAsync(new byte[1].AsMemory(), cancellation.Token)
        );
        Assert.Equal(0, input.Position);
    }

    [Fact]
    public async Task NativeMemoryPathRejectsCorruptChecksum()
    {
        var compressed = Convert.FromBase64String(Checks.Last().Data);
        compressed[compressed.Length - 29] ^= 1;
        using var input = new MemoryStream(compressed);
        using var xz = new XZStream(input);
        using var manager = new NonArrayMemory(new byte[8192]);
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
            while (await xz.ReadAsync(manager.Memory) != 0) { }
        });
    }

#if !DEBUG
    [Fact]
    public async Task SmallMemoryReadsAvoidPerReadTaskAllocations()
    {
        using var input = new MemoryStream(Convert.FromBase64String(Checks.ElementAt(2).Data));
        using var xz = new XZStream(input);
        using var manager = new NonArrayMemory(new byte[16]);
        var memory = manager.Memory;
        var total = await xz.ReadAsync(memory);
        var start = GC.GetAllocatedBytesForCurrentThread();
        int read;
        while ((read = await xz.ReadAsync(memory)) != 0)
        {
            total += read;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(46000, total);
        Assert.InRange(allocated, 0, 65536);
    }
#endif

    private static byte[] Expected() =>
        Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("Memory-backed XZ reads\n", 2000)));

    private sealed class NonArrayMemory(byte[] bytes) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => bytes;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }

    private sealed class YieldingInput(byte[] bytes) : MemoryStream(bytes)
    {
        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return base.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Yield();
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
#endif
