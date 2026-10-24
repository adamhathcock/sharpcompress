using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Filters;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.Xz.Filters;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Xz.Filters;

public class BcjStreamingTests
{
    // Generated with Python's liblzma-backed lzma.compress using the named BCJ filter,
    // then LZMA2 (4 KiB dictionary). Each contains 64 bytes of repeated Pattern plus 01 02 03.
    public static TheoryData<int, byte[], string> FilterData =>
        new()
        {
            {
                0,
                new byte[]
                {
                    0x90,
                    0x90,
                    0xE8,
                    1,
                    0,
                    0,
                    0,
                    0x90,
                    0xE9,
                    0xFA,
                    0xFF,
                    0xFF,
                    0xFF,
                    0x90,
                    0x90,
                    0x90,
                },
                "/Td6WFoAAATm1rRGAgEEACEBAADaM60D4ABCACFdAEhjnBAAx+LdiqQacZbOy2zBK7tSAPV13/5q4ueG1izH4AAAAADxTU9IGTLVxwABPUO/7bulH7bzfQEAAAAABFla"
            },
            {
                1,
                new byte[] { 1, 0, 0, 0xEB },
                "/Td6WFoAAATm1rRGAgEHACEBAAB0QTmF4ABCACZdAAGANhFJcyVDt4tOwKslGS7UlyqO8uC2HEcjUO9htg/bjjkaOIUsAAAANHl680LJGXQAAUJDhogcDR+2830BAAAAAARZWg=="
            },
            {
                2,
                new byte[] { 0, 0xF0, 0, 0xF8 },
                "/Td6WFoAAATm1rRGAgEIACEBAACh82904ABCACddAAA8H1kDYTXTV13anFlyjJQVgF3B02Sm/8oFrVY+LHDbjFEIsfV8SgAA1mGL6Nr8Gi4AAUNDx7kHFB+2830BAAAAAARZWg=="
            },
            {
                3,
                new byte[] { 0x48, 0, 0, 1 },
                "/Td6WFoAAATm1rRGAgEFACEBAAB/4PHI4ABCACZdACQALAMIYG3N7IUSWPRRi6eER96U+fWk3RIfMkA85/N5sxtC5E+AAAAACD4F5qS+FtwAAUJDhogcDR+2830BAAAAAARZWg=="
            },
            {
                4,
                new byte[] { 0x40, 0, 0, 0 },
                "/Td6WFoAAATm1rRGAgEJACEBAAAEIDO/4ABCACVdACAAMCEMA6ITiia3WzaDnt81TgN1nNWCzmIutlB5op8Fjo1IrW8AAAAAHZy7V2smA4MAAUFDRdsxJh+2830BAAAAAARZWg=="
            },
            {
                5,
                new byte[]
                {
                    0x56,
                    0x48,
                    0xB2,
                    0x20,
                    0x39,
                    0xE8,
                    0x44,
                    0x80,
                    0x8C,
                    0x9E,
                    0x2C,
                    0xFC,
                    0x29,
                    0x0E,
                    0x20,
                    0x76,
                },
                "/Td6WFoAAATm1rRGAgEGACEBAADRkmVO4ABCACFdACsSEo/xy020dAkWdmYRw4T59+I+P3S0T08NMexeC9AcAAAAAABwzHp3/6FWBwABPUO/7bulH7bzfQEAAAAABFla"
            },
        };

    public static IEnumerable<object[]> StreamingCases()
    {
        foreach (var data in FilterData)
        {
            foreach (var readSize in new[] { 1, 3, 7, 10240, 1048576 })
            {
                foreach (var sourceReadSize in new[] { 1, 7, 8192 })
                {
                    foreach (var mode in ReadModes())
                    {
                        yield return new object[]
                        {
                            data.Data.Item1,
                            data.Data.Item2,
                            readSize,
                            sourceReadSize,
                            mode,
                        };
                    }
                }
            }
        }
    }

    public static IEnumerable<object[]> XzCases()
    {
        foreach (var data in FilterData)
        {
            foreach (var mode in ReadModes())
            {
                yield return new object[]
                {
                    data.Data.Item1,
                    data.Data.Item2,
                    data.Data.Item3,
                    mode,
                };
            }
        }
    }

    private static IEnumerable<int> ReadModes()
    {
        yield return 0;
        yield return 1;
#if !LEGACY_DOTNET
        yield return 2;
#endif
    }

    [Theory]
    [MemberData(nameof(StreamingCases))]
    public async Task ReadsAcrossInstructionBoundaries(
        int filterId,
        byte[] pattern,
        int readSize,
        int sourceReadSize,
        int mode
    )
    {
        var input = RepeatPattern(pattern, 32768);
        if (filterId == 0)
        {
            // Put a CALL operand across the decoder's internal 8 KiB buffer boundary too.
            Buffer.BlockCopy(new byte[] { 0xE8, 1, 0, 0, 0 }, 0, input, 8190, 5);
        }
        var expected = (byte[])input.Clone();
        ConvertWholeBuffer(filterId, expected);
        Assert.False(input.SequenceEqual(expected));

        using var source = new ShortReadStream(input, sourceReadSize, mode != 0);
        using var forwardOnly = new ForwardOnlyStream(source);
        using var filter = CreateFilter(filterId);
        filter.SetBaseStream(forwardOnly);
        await VerifyReads(filter, expected, readSize, mode);
    }

    [Theory]
    [MemberData(nameof(XzCases))]
    public async Task ReadsLiblzmaEncodedXz(
        int filterId,
        byte[] pattern,
        string compressed,
        int mode
    )
    {
        foreach (var seekable in new[] { true, false })
        {
            using var source = new ShortReadStream(
                Convert.FromBase64String(compressed),
                int.MaxValue,
                mode != 0
            );
            using var forwardOnly = new ForwardOnlyStream(source);
            using var tracked = SharpCompressStream.Create(forwardOnly);
            using var xz = new XZStream(seekable ? source : tracked);
            await VerifyReads(xz, RepeatPattern(pattern, 64), 3, mode);
        }
    }

    [Theory]
    [MemberData(nameof(XzCases))]
    public async Task EmptyReadsAndCancellationDoNotConsumeInput(
        int filterId,
        byte[] pattern,
        string compressed,
        int mode
    )
    {
        using var source = new ShortReadStream(RepeatPattern(pattern, 64), 1, mode != 0);
        using var filter = CreateFilter(filterId);
        filter.SetBaseStream(source);
        var buffer = new byte[8];
        Assert.Equal(0, await Read(filter, buffer, 3, 0, mode, CancellationToken.None));
        Assert.Equal(0, source.Position);

        if (mode != 0)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await Read(filter, buffer, 3, 1, mode, cancellation.Token)
            );
            Assert.Equal(0, source.Position);
        }

        var expected = RepeatPattern(pattern, 64);
        ConvertWholeBuffer(filterId, expected);
        await VerifyReads(filter, expected, 7, mode);
        filter.Dispose();
        Assert.True(source.CanRead);
    }

    [Theory]
    [MemberData(nameof(XzCases))]
    public async Task ReadsShortStreamsAndIncompleteFinalInstructions(
        int filterId,
        byte[] pattern,
        string compressed,
        int mode
    )
    {
        foreach (var length in new[] { 0, 1, 2, 3, 4, 5, 7, 15, 16, 17, 31, 32 })
        {
            var input = RepeatPattern(pattern, 64).Take(length).ToArray();
            var expected = (byte[])input.Clone();
            // The public IA-64 one-shot converter rejects short buffers, but a streaming
            // decoder must pass an incomplete final bundle through unchanged.
            if (filterId != 5 || length >= 16)
            {
                ConvertWholeBuffer(filterId, expected);
            }
            using var source = new ShortReadStream(input, 1, mode != 0);
            using var filter = CreateFilter(filterId);
            filter.SetBaseStream(source);
            await VerifyReads(filter, expected, 3, mode);
        }
    }

    private static byte[] RepeatPattern(byte[] pattern, int length)
    {
        var result = new byte[length + 3];
        for (var offset = 0; offset < length; offset += pattern.Length)
        {
            Buffer.BlockCopy(pattern, 0, result, offset, pattern.Length);
        }
        result[length] = 1;
        result[length + 1] = 2;
        result[length + 2] = 3;
        return result;
    }

    private static BlockFilter CreateFilter(int filterId) =>
        filterId switch
        {
            0 => new X86Filter(),
            1 => new ArmFilter(),
            2 => new ArmThumbFilter(),
            3 => new PowerPCFilter(),
            4 => new SparcFilter(),
            5 => new IA64Filter(),
            _ => throw new ArgumentOutOfRangeException(nameof(filterId)),
        };

    private static void ConvertWholeBuffer(int filterId, byte[] data)
    {
        switch (filterId)
        {
            case 0:
                uint state = 0;
                BranchExecFilter.X86Converter(data, 0, ref state);
                break;
            case 1:
                BranchExecFilter.ARMConverter(data, 0);
                break;
            case 2:
                BranchExecFilter.ARMTConverter(data, 0);
                break;
            case 3:
                BranchExecFilter.PowerPCConverter(data, 0);
                break;
            case 4:
                BranchExecFilter.SPARCConverter(data, 0);
                break;
            case 5:
                BranchExecFilter.IA64Converter(data, 0);
                break;
        }
    }

    private static async Task VerifyReads(Stream stream, byte[] expected, int readSize, int mode)
    {
        const int offset = 5;
        var buffer = new byte[readSize + offset + 8];
        using var output = new MemoryStream();
        while (true)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 0xCC;
            }
            var read = await Read(stream, buffer, offset, readSize, mode, CancellationToken.None);
            Assert.True(buffer.Take(offset).All(value => value == 0xCC));
            Assert.True(buffer.Skip(offset + read).All(value => value == 0xCC));
            if (read == 0)
            {
                break;
            }
            // A short read is only valid at EOF in the XZ filter chain.
            Assert.Equal(Math.Min(readSize, expected.Length - (int)output.Length), read);
            output.Write(buffer, offset, read);
        }
        Assert.Equal(expected, output.ToArray());
        Assert.Equal(0, await Read(stream, buffer, offset, readSize, mode, CancellationToken.None));
    }

    private static async Task<int> Read(
        Stream stream,
        byte[] buffer,
        int offset,
        int count,
        int mode,
        CancellationToken cancellationToken
    )
    {
        switch (mode)
        {
            case 0:
                return stream.Read(buffer, offset, count);
            case 1:
                return await stream.ReadAsync(buffer, offset, count, cancellationToken);
#if !LEGACY_DOTNET
            case 2:
                return await stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
#endif
            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private sealed class ShortReadStream : MemoryStream
    {
#if !LEGACY_DOTNET
        private readonly byte[] _data;
#endif
        private readonly int _maxRead;
        private readonly bool _asyncOnly;

        public ShortReadStream(byte[] data, int maxRead, bool asyncOnly)
            : base(data)
        {
#if !LEGACY_DOTNET
            _data = data;
#endif
            _maxRead = maxRead;
            _asyncOnly = asyncOnly;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Assert.False(_asyncOnly);
            return base.Read(buffer, offset, Math.Min(count, _maxRead));
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(base.Read(buffer, offset, Math.Min(count, _maxRead)));
        }

#if !LEGACY_DOTNET
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(Math.Min(buffer.Length, _maxRead), Length - Position);
            _data.AsMemory((int)Position, count).CopyTo(buffer);
            Position += count;
            return new ValueTask<int>(count);
        }
#endif
    }
}
