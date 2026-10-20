#if !LEGACY_DOTNET
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.PPMd;
using Xunit;

namespace SharpCompress.Test.Streams;

public class MemoryEncoderWriteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Memory_Writes_Match_Array_Writes(bool ppmd)
    {
        var data = new byte[20000];
        new Random(42).NextBytes(data);
        using var memory = new TestMemoryManager(data);
        using var expected = new MemoryStream();
        using var actual = new MemoryStream();
        await Encode(expected, false);
        await Encode(actual, true);
        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(data, memory.GetSpan().ToArray());

        async Task Encode(Stream output, bool useMemory)
        {
            await using Stream encoder = ppmd
                ? PpmdStream.Create(new PpmdProperties(), output, true)
                : LzmaStream.Create(new LzmaEncoderProperties(true, 4096), false, output);
            for (var offset = 1; offset < data.Length - 1; )
            {
                var count = Math.Min(137, data.Length - 1 - offset);
                if (useMemory)
                {
                    await encoder.WriteAsync(
                        memory.Memory.Slice(offset, count),
                        CancellationToken.None
                    );
                }
                else
                {
                    await encoder.WriteAsync(data, offset, count, CancellationToken.None);
                }
                offset += count;
            }
            await encoder.WriteAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        }
    }

    // Exercises memory that cannot be unwrapped with MemoryMarshal.TryGetArray.
    private sealed class TestMemoryManager(byte[] data) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => data;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}
#endif
