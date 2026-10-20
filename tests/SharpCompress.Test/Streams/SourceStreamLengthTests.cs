using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Streams;

/// <summary>
/// SourceStream bounds every read by the current part's length; reading Stream.Length can be a
/// system call per access (fstat for a FileStream on Unix), so each part's length must be read once.
/// </summary>
public class SourceStreamLengthTests
{
    private const int ChunkSize = 7;

    private static readonly byte[] Data = Enumerable
        .Range(0, 1000)
        .Select(i => (byte)(i % 251))
        .ToArray();

    [Fact]
    public void Read_SinglePart_ReadsLengthOnce()
    {
        var part = new LengthCountingStream(Data);
        using var source = new SourceStream(part, _ => null, ReaderOptions.ForExternalStream);

        var read = ReadAll(source);

        read.Should().Equal(Data);
        part.LengthReads.Should().Be(1);
    }

    [Fact]
    public async Task ReadAsync_SinglePart_ReadsLengthOnce()
    {
        var part = new LengthCountingStream(Data);
        using var source = new SourceStream(part, _ => null, ReaderOptions.ForExternalStream);

        var read = await ReadAllAsync(source, useMemory: false);

        read.Should().Equal(Data);
        part.LengthReads.Should().Be(1);
    }

#if !LEGACY_DOTNET
    [Fact]
    public async Task ReadAsyncMemory_SinglePart_ReadsLengthOnce()
    {
        var part = new LengthCountingStream(Data);
        using var source = new SourceStream(part, _ => null, ReaderOptions.ForExternalStream);

        var read = await ReadAllAsync(source, useMemory: true);

        read.Should().Equal(Data);
        part.LengthReads.Should().Be(1);
    }
#endif

    [Fact]
    public void Read_MultipleParts_ReadsEachLengthOnce()
    {
        var first = new LengthCountingStream(Data.Take(400).ToArray());
        var second = new LengthCountingStream(Data.Skip(400).ToArray());
        using var source = new SourceStream(
            first,
            i => i == 1 ? second : null,
            ReaderOptions.ForExternalStream
        );

        var read = ReadAll(source);

        read.Should().Equal(Data);
        first.LengthReads.Should().Be(1);
        second.LengthReads.Should().Be(1);
    }

    [Fact]
    public void LengthAndSeek_MultipleParts_ReadEachLengthOnce()
    {
        var first = new LengthCountingStream(Data.Take(400).ToArray());
        var second = new LengthCountingStream(Data.Skip(400).ToArray());
        using var source = new SourceStream(
            first,
            i => i == 1 ? second : null,
            ReaderOptions.ForExternalStream
        );
        source.LoadAllParts();

        var length = source.Length;
        source.Seek(600, SeekOrigin.Begin);
        var afterBoundary = source.ReadByte();
        source.Seek(-10, SeekOrigin.End);
        var nearEnd = source.ReadByte();
        source.Seek(100, SeekOrigin.Begin);
        var beforeBoundary = source.ReadByte();

        length.Should().Be(Data.Length);
        source.Length.Should().Be(Data.Length);
        afterBoundary.Should().Be(Data[600]);
        nearEnd.Should().Be(Data[Data.Length - 10]);
        beforeBoundary.Should().Be(Data[100]);
        first.LengthReads.Should().Be(1);
        second.LengthReads.Should().Be(1);
    }

    private static byte[] ReadAll(Stream source)
    {
        using var result = new MemoryStream();
        var buffer = new byte[ChunkSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            result.Write(buffer, 0, read);
        }
        return result.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream source, bool useMemory)
    {
        using var result = new MemoryStream();
        var buffer = new byte[ChunkSize];
        while (true)
        {
#if !LEGACY_DOTNET
            var read = useMemory
                ? await source.ReadAsync(buffer.AsMemory())
                : await source.ReadAsync(buffer, 0, buffer.Length);
#else
            var read = await source.ReadAsync(buffer, 0, buffer.Length);
#endif
            if (read == 0)
            {
                return result.ToArray();
            }
            result.Write(buffer, 0, read);
        }
    }

    private sealed class LengthCountingStream(byte[] data) : MemoryStream(data, writable: false)
    {
        public int LengthReads { get; private set; }

        public override long Length
        {
            get
            {
                LengthReads++;
                return base.Length;
            }
        }
    }
}
