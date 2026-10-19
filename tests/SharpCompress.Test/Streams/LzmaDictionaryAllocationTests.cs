using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.LZ;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzmaDictionaryAllocationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Lzma_Raw_Huge_Dictionary_Decodes_With_Stream_And_Direct_APIs(
        bool useAsync,
        bool direct
    )
    {
        var expected = new byte[20000];
        new Random(123).NextBytes(expected);
        using var packed = new MemoryStream();
        byte[] properties;
        using (
            var encoder = LzmaStream.Create(new LzmaEncoderProperties(false, 4096), false, packed)
        )
        {
            properties = (byte[])encoder.Properties.Clone();
            encoder.Write(expected, 0, expected.Length);
        }
        // The compressed payload needs only a small dictionary, regardless of its advertised size.
        properties[1] = 255;
        properties[2] = 255;
        properties[3] = 255;
        properties[4] = 127;
        using var input = new MemoryStream(packed.ToArray());
        using var output = new MemoryStream();
        if (direct)
        {
            using var decoder = new Decoder();
            decoder.SetDecoderProperties(properties);
            if (useAsync)
            {
                await decoder.CodeAsync(
                    input,
                    output,
                    input.Length,
                    expected.Length,
                    null!,
                    CancellationToken.None
                );
            }
            else
            {
                decoder.Code(input, output, input.Length, expected.Length, null!);
            }
        }
        else if (useAsync)
        {
            using var decoder = await LzmaStream.CreateAsync(
                properties,
                input,
                input.Length,
                expected.Length
            );
            await decoder.CopyToAsync(output, 137, CancellationToken.None);
        }
        else
        {
            using var decoder = LzmaStream.Create(properties, input, input.Length, expected.Length);
            decoder.CopyTo(output, 137);
        }
        Assert.Equal(expected, output.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma_Truncated_Uncompressed_Chunk_Does_Not_Allocate_From_Read_Size(
        bool useAsync
    )
    {
        using var window = new OutWindow();
        window.Create(int.MaxValue);
        window.SetLimit(int.MaxValue);
        using var input = new MemoryStream(new byte[] { 1 });
        if (useAsync)
        {
            await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
                await window.CopyStreamAsync(input, int.MaxValue, CancellationToken.None)
            );
        }
        else
        {
            Assert.ThrowsAny<SharpCompressException>(() => window.CopyStream(input, int.MaxValue));
        }
        Assert.InRange(window.BufferCapacity, 1, 8192);
    }

    [Theory]
    [InlineData(38)]
    [InlineData(40)]
    [InlineData(64)]
    [InlineData(255)]
    public void Lzma2_Rejects_Unsupported_Dictionary_Properties(int property)
    {
        Assert.ThrowsAny<SharpCompressException>(() =>
            LzmaStream.Create(new byte[] { (byte)property }, Stream.Null)
        );
    }

    [Fact]
    public void Lzma_Huge_Dictionary_Starts_Small_And_Grows_With_History()
    {
        using var window = new OutWindow();
        window.Create(int.MaxValue);
        Assert.InRange(window.BufferCapacity, 1, 4096);
        Assert.Equal(0, window.GetByte(0));
        window.SetLimit(20000);
        for (var i = 0; i < 20000; i++)
        {
            window.PutByte((byte)i);
        }
        Assert.InRange(window.BufferCapacity, 20000, 32768);
        for (var i = 0; i < 20000; i++)
        {
            Assert.Equal((byte)i, window.GetByte(19999 - i));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma_History_Survives_Growth_Wrap_And_Pending_Matches(bool useAsync)
    {
        using var window = new OutWindow();
        window.Create(8192);
        var output = new byte[8192];
        window.SetLimit(8192);
        for (var i = 0; i < output.Length; i++)
        {
            if (useAsync)
            {
                await window.PutByteAsync((byte)i, CancellationToken.None);
            }
            else
            {
                window.PutByte((byte)i);
            }
        }
        Assert.Equal(output.Length, window.Read(output, 0, output.Length));
        window.SetLimit(10);
        if (useAsync)
        {
            await window.CopyBlockAsync(8191, 20, CancellationToken.None);
        }
        else
        {
            window.CopyBlock(8191, 20);
        }
        Assert.True(window.HasPending);
        window.SetLimit(10);
        if (useAsync)
        {
            await window.CopyPendingAsync(CancellationToken.None);
        }
        else
        {
            window.CopyPending();
        }
        Assert.False(window.HasPending);
        Assert.Equal(20, window.Read(output, 0, output.Length));
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal((byte)i, output[i]);
        }
        window.Reset();
        Assert.Equal(0, window.Total);
        Assert.Equal(0, window.GetByte(0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma_Preset_Dictionary_Does_Not_Allocate_Declared_Window(bool useAsync)
    {
        using var window = new OutWindow();
        window.Create(int.MaxValue);
        using var preset = new MemoryStream(new byte[] { 1, 2, 3 });
        if (useAsync)
        {
            await window.TrainAsync(preset);
        }
        else
        {
            window.Train(preset);
        }
        Assert.Equal(3, window.GetByte(0));
        Assert.Equal(1, window.GetByte(2));
        Assert.InRange(window.BufferCapacity, 3, 4096);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma2_Tiny_Stream_With_Huge_Dictionary_Decodes(bool useAsync)
    {
        using var input = new MemoryStream(new byte[] { 1, 0, 0, 65, 0 });
        using var output = new MemoryStream();
        if (useAsync)
        {
            using var decoder = await LzmaStream.CreateAsync(new byte[] { 37 }, input, -1, -1);
            await decoder.CopyToAsync(output, 81920, CancellationToken.None);
        }
        else
        {
            using var decoder = LzmaStream.Create(new byte[] { 37 }, input);
            decoder.CopyTo(output);
        }
        Assert.Equal(new byte[] { 65 }, output.ToArray());
    }
}
