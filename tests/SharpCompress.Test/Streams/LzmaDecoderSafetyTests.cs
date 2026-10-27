using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzmaDecoderSafetyTests
{
    // Corruption followed by another read, including the implicit retry during entry skipping.
    private const string RereadHex =
        "fd377a585a0000016922de360200210100000000372797d6010011616c7068610a616c7068610a616c70"
        + "10000afff3003f0e7b9e00012600100000109042990d010000000001595a";

    private const string TarGzipBase64 =
        "H4sIAAAAAAACCvtrXhURxcDAmKl0z4yJQZGRAQjM1adfY5T8r6cfkFjhkZqYklpUrGdoZmJkqK/HQGVgAARmJiZgGgjQaQMD"
        + "UyM09ebmJuYMChUMdAClxSWJRUArSdDyHwgosZIy3dQFhiYKBYklGbaJeiUVJVxGFgq5JZm5qbaG5pZGBiYGFoaWeqbGxsam"
        + "xhZcDEMY+DGMAuwAHO80toNg/jcyYjA0NTIzMTA1NzUzBoqbAQsABgWDQZr/h1X85xRkJHIhk/hUsww5/30azeIDD/AWAIag"
        + "BoAhZgPADKUBQLt2CoUFQMOgDnnzqj3r1WWIaAYYQ5oBKZlF+jhbAeZGhlyjiXlYAlC806MUMDc1xd0MMFBHbwaYmgO7Aab0"
        + "bwY8pVvAMw6O+B8c3UAzjFrA1JiW3UA9Pf2h1xIM8GZkoklP0AJRBSTh7Q1amJiP1gMQ8IzAUIDS0KoCkmjcGyTcEzRBqwKA"
        + "FYbpSOsJBnizslHLLGHSLGZmSVRISi1BIoZGTk8ZSYVOA7zQERuKzn/KMAqoAXiHprPrB8ba/99A5NfRZIMK7qNytX/dEWRg"
        + "nGLSYMLAsDPV4r6dAW83uLXJGBkFAFQcy31GGgAA";

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 65536)]
    [InlineData(true, 1)]
    [InlineData(true, 65536)]
    public void CorruptInputThrowsDataError(bool raw, int bufferSize)
    {
        using var input = CreateInput(raw);
        using var stream = CreateStream(input, raw);
        var buffer = new byte[bufferSize];
        Assert.Throws<DataErrorException>(() => Drain(stream, buffer));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadByteCorruptInputThrowsDataError(bool raw)
    {
        using var input = CreateInput(raw);
        using var stream = CreateStream(input, raw);
        Assert.Throws<DataErrorException>(() =>
        {
            for (var count = 0; count < 65536; count++)
            {
                Assert.NotEqual(-1, stream.ReadByte());
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncCorruptInputThrowsDataError(bool raw)
    {
        using var input = CreateInput(raw);
        using var stream = await CreateStreamAsync(input, raw);
        var buffer = new byte[65536];
        await Assert.ThrowsAsync<DataErrorException>(async () =>
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                Assert.NotEqual(
                    0,
                    await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
                );
            }
        });
    }

    // LZMA2 chunk with dictionary and properties reset whose properties byte encodes pb > 4.
    private static readonly byte[] InvalidChunkProperties = { 0xE0, 0, 0, 0, 4, 0xFF, 0, 0, 0, 0 };

    [Fact]
    public void Lzma2InvalidChunkPropertiesThrowsDataError()
    {
        using var input = new MemoryStream(InvalidChunkProperties);
        using var stream = LzmaStream.Create(new byte[] { 0 }, input, leaveOpen: true);
        Assert.Throws<DataErrorException>(() => stream.ReadByte());
    }

    [Fact]
    public async Task AsyncLzma2InvalidChunkPropertiesThrowsDataError()
    {
        using var input = new MemoryStream(InvalidChunkProperties);
        using var stream = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        var buffer = new byte[16];
        await Assert.ThrowsAsync<DataErrorException>(() =>
            stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
        );
    }

#if !LEGACY_DOTNET
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncMemoryCorruptInputThrowsDataError(bool raw)
    {
        using var input = CreateInput(raw);
        using var stream = await CreateStreamAsync(input, raw);
        var buffer = new byte[65536];
        await Assert.ThrowsAsync<DataErrorException>(async () =>
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                Assert.NotEqual(
                    0,
                    await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None)
                );
            }
        });
    }

    [Fact]
    public void FastDecoderRetryAfterCorruptionRejectsInvalidDistance()
    {
        using var input = CreateInput(true);
        using var stream = LzmaStream.Create(new byte[] { 0 }, input, leaveOpen: true);
        Assert.Throws<DataErrorException>(() => Drain(stream, new byte[65536]));

        // Exercise the unsafe decoder directly after corruption to verify its bounds checks.
        var decoder = GetField<Decoder>(stream, "_decoder");
        var window = GetField<OutWindow>(stream, "_outWindow");
        var range = GetField<SharpCompress.Compressors.LZMA.RangeCoder.Decoder>(
            stream,
            "_rangeDecoder"
        );
        try
        {
            Assert.Throws<DataErrorException>(() => decoder.CodeFast(4096, window, range));
        }
        finally
        {
            range.ReleaseStream();
        }
    }

    [Theory]
    [InlineData(1u)] // Inside the window, but outside the available history.
    [InlineData(4096u)]
    [InlineData(0x80000000u)]
    [InlineData(uint.MaxValue)]
    public void FastDecoderRejectsInvalidMatchedLiteralDistance(uint distance)
    {
        using var decoder = new Decoder();
        decoder.SetDecoderProperties(new byte[] { 0x5D, 0, 0x10, 0, 0 });
        var state = new Base.State();
        state.UpdateMatch();
        SetField(decoder, "_state", state);
        SetField(decoder, "_rep0", distance);
        using var window = new OutWindow();
        window.Create(4096);
        window.SetLimit(2);
        window.PutByte(0);
        var range = new SharpCompress.Compressors.LZMA.RangeCoder.Decoder();
        try
        {
            Assert.Throws<DataErrorException>(() => decoder.CodeFast(4096, window, range));
        }
        finally
        {
            range.ReleaseStream();
        }
    }

    private static T GetField<T>(object instance, string name) =>
        (T)
            instance
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(instance)!;

    [Theory]
    [InlineData(0u)]
    [InlineData(4095u)]
    public void FastDecoderAcceptsTrainedHistoryAtDictionaryWrap(uint distance)
    {
        using var decoder = new Decoder();
        decoder.SetDecoderProperties(new byte[] { 0x5D, 0, 0x10, 0, 0 });
        var state = new Base.State();
        state.UpdateMatch();
        SetField(decoder, "_state", state);
        SetField(decoder, "_rep0", distance);
        using var window = new OutWindow();
        window.Create(4096);
        using var dictionary = new MemoryStream(new byte[4096]);
        window.Train(dictionary);
        window.SetLimit(1);
        using var input = new MemoryStream(new byte[16]);
        var range = new SharpCompress.Compressors.LZMA.RangeCoder.Decoder();
        range.Init(input);
        try
        {
            Assert.False(decoder.CodeFast(4096, window, range));
            Assert.Equal(4097, window.Total);
            Assert.Equal(0, window.ReadByte());
        }
        finally
        {
            range.ReleaseStream();
        }
    }

    private static void SetField<T>(object instance, string name, T value) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
#endif

    [Fact]
    public void ReaderSkippingCorruptTarXzThrowsManagedException()
    {
        using var packed = new MemoryStream(Convert.FromBase64String(TarGzipBase64));
        using var gzip = new GZipStream(packed, CompressionMode.Decompress);
        using var input = new MemoryStream();
        gzip.CopyTo(input);
        input.Position = 0;
        Assert.ThrowsAny<SharpCompressException>(() =>
        {
            using var reader = ReaderFactory.OpenReader(input, ReaderOptions.ForExternalStream);
            for (var entry = 0; entry < 32; entry++)
            {
                Assert.True(reader.MoveToNextEntry());
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FaultedStreamDisposalPreservesOwnership(bool leaveOpen)
    {
        using var input = CreateInput(true);
        var stream = LzmaStream.Create(new byte[] { 0 }, input, leaveOpen: leaveOpen);
        Assert.Throws<DataErrorException>(() => Drain(stream, new byte[65536]));
        stream.Dispose();
        stream.Dispose();
        Assert.Equal(leaveOpen, input.CanRead);
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FaultedStreamAsyncDisposalPreservesOwnership(bool leaveOpen)
    {
        using var input = CreateInput(true);
        var stream = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: leaveOpen
        );
        var buffer = new byte[65536];
        await Assert.ThrowsAsync<DataErrorException>(() =>
            stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
        );
        await stream.DisposeAsync();
        await stream.DisposeAsync();
        Assert.Equal(leaveOpen, input.CanRead);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CancellationBeforeDecodingDoesNotFaultStream()
    {
        using var input = new MemoryStream(new byte[] { 1, 0, 0, (byte)'X', 0 });
        await using var stream = await LzmaStream.CreateAsync(
            new byte[] { 0 },
            input,
            -1,
            -1,
            leaveOpen: true
        );
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var buffer = new byte[2];
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            stream.ReadAsync(buffer, 0, buffer.Length, cancellation.Token)
        );
        Assert.Equal(0, input.Position);
        Assert.Equal(1, await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None));
        Assert.Equal((byte)'X', buffer[0]);
    }

#if !LEGACY_DOTNET
    [Fact]
    public void RawLzmaRejectsShortRepeatWithoutHistory()
    {
        // Initial range code selects IsMatch=1, IsRep=1, IsRepG0=0, IsRep0Long=0.
        using var input = new MemoryStream(new byte[] { 0, 0xC0, 0, 0, 0 });
        using var stream = LzmaStream.Create(
            new byte[] { 0x5D, 0, 0x10, 0, 0 },
            input,
            leaveOpen: true
        );
        Assert.Throws<DataErrorException>(() => stream.ReadByte());
        Assert.Equal(5, input.Position);
    }
#endif

    private static MemoryStream CreateInput(bool raw)
    {
        var bytes = new byte[RereadHex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(RereadHex.Substring(i * 2, 2), 16);
        }
        return new MemoryStream(raw ? bytes.AsSpan(24).ToArray() : bytes);
    }

    private static Stream CreateStream(Stream input, bool raw) =>
        raw ? LzmaStream.Create(new byte[] { 0 }, input, leaveOpen: true) : new XZStream(input);

    private static async Task<Stream> CreateStreamAsync(Stream input, bool raw) =>
        raw
            ? await LzmaStream.CreateAsync(new byte[] { 0 }, input, -1, -1, leaveOpen: true)
            : new XZStream(input);

    private static void Drain(Stream stream, byte[] buffer)
    {
        for (var attempt = 0; attempt < 65536; attempt++)
        {
            Assert.NotEqual(0, stream.Read(buffer, 0, buffer.Length));
        }
    }
}
