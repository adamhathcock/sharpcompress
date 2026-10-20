using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzmaScratchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Raw_Decode_Failure_Releases_The_Dictionary(bool useAsync)
    {
        using var decoder = new SharpCompress.Compressors.LZMA.Decoder();
        decoder.SetDecoderProperties(new byte[] { 0x5D, 0, 0, 1, 0 });
        using var input = new FailingInput();
        if (useAsync)
        {
            await Assert.ThrowsAsync<IOException>(async () =>
                await decoder.CodeAsync(input, Stream.Null, 32, 4096, null!, CancellationToken.None)
            );
        }
        else
        {
            Assert.Throws<IOException>(() => decoder.Code(input, Stream.Null, 32, 4096, null!));
        }
        Assert.Null(
            decoder
                .GetType()
                .GetField("_outWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(decoder)
        );
    }

    private sealed class FailingInput : MemoryStream
    {
        public override int ReadByte() => throw new IOException("Read failure");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => throw new IOException("Read failure");

#if !LEGACY_DOTNET
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => throw new IOException("Read failure");
#endif
    }

    [Theory]
    [InlineData(false)]
#if !LEGACY_DOTNET
    [InlineData(true)]
#endif
    public void Smaller_Literal_Properties_Release_Oversized_Banks(bool fast)
    {
        using var decoder = new SharpCompress.Compressors.LZMA.Decoder();
        object target = decoder;
        var methodName = "CreateFastModel";
        var fieldName = "_fLiteral";
        if (!fast)
        {
            target = decoder
                .GetType()
                .GetField("_literalDecoder", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(decoder)!;
            methodName = "Create";
            fieldName = "_models";
        }
        var method = target
            .GetType()
            .GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            )!;
        var field = target
            .GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(target, new object[] { 0, 8 });
        var large = (Array)field.GetValue(target)!;
        method.Invoke(target, new object[] { 0, 0 });
        var small = (Array)field.GetValue(target)!;
        Assert.True(small.Length < large.Length);
        Assert.InRange(small.Length, 768, 4096);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(17)]
    public void Digests_Keep_Partial_Vector_And_Little_Endian_Encoding(int count)
    {
        var crcs = Enumerable
            .Range(0, count)
            .Select(i => i % 3 == 0 ? (uint?)(0x12345678u + (uint)i) : null)
            .ToArray();
        using var expected = new MemoryStream();
        expected.WriteOptionalBoolVector(crcs.Select(c => c.HasValue).ToArray());
        foreach (var crc in crcs)
        {
            if (crc is { } value)
            {
                var bytes = new byte[4];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
                expected.Write(bytes, 0, bytes.Length);
            }
        }
        using var actual = new MemoryStream();
        new SevenZipDigestsWriter(crcs).Write(actual);
        Assert.Equal(expected.ToArray(), actual.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_Decode_Disposal_Releases_Literal_Models(bool useAsync)
    {
        using var packed = new MemoryStream();
        var encoder = new Lzma2EncoderStream(packed, 1 << 16, 32);
        var properties = encoder.Properties;
        using (encoder)
        {
            encoder.Write(new byte[8192], 0, 8192);
        }
        packed.Position = 0;
        using var stream = LzmaStream.Create(
            properties,
            packed,
            packed.Length,
            8192,
            leaveOpen: true
        );
        var bytes = new byte[1];
        if (useAsync)
        {
            await stream.ReadAsync(bytes, 0, 1, CancellationToken.None);
        }
        else
        {
            stream.Read(bytes, 0, 1);
        }
        var decoder = stream
            .GetType()
            .GetField("_decoder", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(stream)!;
        if (useAsync)
        {
            await stream.DisposeAsync();
        }
        else
        {
            stream.Dispose();
        }
        stream.Dispose();
        var literal = decoder
            .GetType()
            .GetField("_literalDecoder", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(decoder)!;
        Assert.Null(
            literal
                .GetType()
                .GetField("_models", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(literal)
        );
#if !LEGACY_DOTNET
        Assert.Null(
            decoder
                .GetType()
                .GetField("_fLiteral", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(decoder)
        );
#endif
        Assert.True(packed.CanRead);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Lzma2_Multiple_Blocks_RoundTrip(bool useAsync, bool random)
    {
        var expected = new byte[4 * 1024 * 1024 + 17];
        if (random)
        {
            new Random(987).NextBytes(expected);
        }
        else
        {
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = (byte)(i % 17);
            }
        }
        using var packed = new MemoryStream();
        byte[] properties;
        var encoder = new Lzma2EncoderStream(packed, 1 << 16, 32);
        properties = encoder.Properties;
        if (useAsync)
        {
            await using (encoder)
            {
                await encoder.WriteAsync(expected, 0, expected.Length, CancellationToken.None);
            }
        }
        else
        {
            using (encoder)
            {
                encoder.Write(expected, 0, expected.Length);
            }
        }
        var trial = encoder
            .GetType()
            .GetField("_trialDecoder", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(encoder)!;
        var literals = trial
            .GetType()
            .GetField("_literalDecoder", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(trial)!;
        Assert.Null(
            literals
                .GetType()
                .GetField("_models", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(literals)
        );
#if !LEGACY_DOTNET
        Assert.Null(
            trial
                .GetType()
                .GetField("_fLiteral", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(trial)
        );
#endif
        packed.Position = 0;
        using var decoder = LzmaStream.Create(
            properties,
            packed,
            packed.Length,
            expected.Length,
            leaveOpen: true
        );
        using var actual = new MemoryStream();
        if (useAsync)
        {
            await decoder.CopyToAsync(actual, 65536, CancellationToken.None);
        }
        else
        {
            decoder.CopyTo(actual);
        }
        Assert.Equal(expected, actual.ToArray());
        Assert.True(packed.CanRead);
    }

#if !LEGACY_DOTNET
    [Fact]
    public async Task Lzma_Memory_Write_Borrows_Only_The_Requested_Slice()
    {
        var bytes = new byte[8192];
        new Random(654).NextBytes(bytes);
        var expected = Enumerable.Range(0, 4).SelectMany(_ => bytes.Skip(7).Take(4096)).ToArray();
        using var memory = new NonArrayMemory(bytes);
        using var packed = new MemoryStream();
        var encoder = LzmaStream.Create(new LzmaEncoderProperties(true, 1 << 16), false, packed);
        var properties = encoder.Properties;
        await using (encoder)
        {
            for (var i = 0; i < 4; i++)
            {
                await encoder.WriteAsync(memory.Memory.Slice(7, 4096), CancellationToken.None);
            }
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await encoder.WriteAsync(bytes.AsMemory(), cancelled.Token)
            );
            // Caller memory may be reused as soon as the awaited write completes,
            // even though encoder finalization still has pending output.
            Array.Clear(bytes, 0, bytes.Length);
        }
        packed.Position = 0;
        using var decoder = LzmaStream.Create(
            properties,
            packed,
            packed.Length,
            16384,
            leaveOpen: true
        );
        using var actual = new MemoryStream();
        await decoder.CopyToAsync(actual, 65536, CancellationToken.None);
        Assert.Equal(expected, actual.ToArray());
    }
#endif

    private sealed class NonArrayMemory(byte[] bytes) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => bytes;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }

    [Fact]
    public void Encoder_Workspace_Is_Lazy_And_Returned_On_Dispose()
    {
        using var encoder = new SharpCompress.Compressors.LZMA.Encoder();
        var field = encoder
            .GetType()
            .GetField("_optimum", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(field.GetValue(encoder));
        using var input = new MemoryStream(new byte[4096]);
        encoder.Code(input, Stream.Null, input.Length, -1, null!);
        Assert.NotNull(field.GetValue(encoder));
        encoder.Dispose();
        encoder.Dispose();
        Assert.Null(field.GetValue(encoder));
    }

    [Fact]
    public void FilesInfo_Streams_Names_And_Partial_Metadata()
    {
        var entries = Enumerable
            .Range(0, 17)
            .Select(i => new SevenZipWriteEntry
            {
                Name = i + "-\ud83d\ude00-\ud800-" + new string('x', i % 2 == 0 ? 300 : 5),
                IsEmpty = true,
                IsDirectory = i % 3 == 0,
                ModificationTime =
                    i % 2 == 0 ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
                Attributes = i % 2 == 0 ? 32u : null,
            })
            .ToArray();
        using var stream = new MemoryStream();
        new SevenZipFilesInfoWriter { Entries = entries }.Write(stream);
        var bytes = stream.ToArray();
        var reader = new DataReader(bytes, 0, bytes.Length);
        Assert.Equal(17, reader.ReadNum());
        while (true)
        {
            var property = (BlockType)reader.ReadByte();
            if (property == BlockType.End)
            {
                break;
            }
            var payload = new byte[reader.ReadNum()];
            reader.ReadBytes(payload, 0, payload.Length);
            if (property == BlockType.Name)
            {
                Assert.Equal(0, payload[0]);
                Assert.Equal(
                    Encoding.Unicode.GetBytes(string.Concat(entries.Select(e => e.Name + "\0"))),
                    payload.Skip(1)
                );
            }
            else if (property == BlockType.MTime || property == BlockType.WinAttributes)
            {
                Assert.Equal(new byte[] { 0, 0xAA, 0xAA, 0x80, 0 }, payload.Take(5));
                Assert.Equal(5 + 9 * (property == BlockType.MTime ? 8 : 4), payload.Length);
            }
        }
        Assert.Equal(bytes.Length, reader.Offset);
    }
}
