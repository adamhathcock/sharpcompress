using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class RewindableStreamAsyncTest
{
    [Fact]
    public async ValueTask TestRewindAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        stream.Rewind(true);
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));
    }

    [Fact]
    public async ValueTask TestIncompleteRewindAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        stream.Rewind(true);
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));
    }

    [Fact]
    public async ValueTask TestRecordingAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        stream.Rewind(false);
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
    }

    [Fact]
    public async ValueTask TestAsyncProducesSameResultAsSync()
    {
        var testData = new byte[100 * 4];
        for (int i = 0; i < 100; i++)
        {
            var bytes = BitConverter.GetBytes(i);
            Array.Copy(bytes, 0, testData, i * 4, 4);
        }

        byte[] syncResult;
        byte[] asyncResult;

        var ms1 = new MemoryStream(testData);
        using (var stream = new RewindableStream(ms1))
        {
            syncResult = ReadAllSync(stream);
        }

        var ms2 = new MemoryStream(testData);
        using (var stream = new RewindableStream(ms2))
        {
            asyncResult = await ReadAllAsync(stream).ConfigureAwait(false);
        }

        Assert.Equal(syncResult, asyncResult);
    }

    [Fact]
    public async ValueTask TestAsyncWithRewind()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 50; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();

        var buffer = new byte[8];
        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(0, BitConverter.ToInt32(buffer, 0));
        Assert.Equal(1, BitConverter.ToInt32(buffer, 4));

        stream.Rewind(false);
        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(0, BitConverter.ToInt32(buffer, 0));
        Assert.Equal(1, BitConverter.ToInt32(buffer, 4));

        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(2, BitConverter.ToInt32(buffer, 0));
        Assert.Equal(3, BitConverter.ToInt32(buffer, 4));
    }

    [Fact]
    public async ValueTask TestAsyncCancellationSupport()
    {
        var ms = new MemoryStream(new byte[10000]);
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var cts = new CancellationTokenSource();
        var buffer = new byte[4096];

        // Just verify that cancellation token can be passed without throwing
        int bytesRead = await stream
            .ReadAsync(buffer, 0, buffer.Length, cts.Token)
            .ConfigureAwait(false);
        Assert.Equal(buffer.Length, bytesRead);
    }

    [Fact]
    public async ValueTask TestAsyncEmptyBuffer()
    {
        var ms = new MemoryStream();
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var buffer = new byte[0];
        int bytesRead = await stream.ReadAsync(buffer, 0, 0).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask TestAsyncMultipleReads()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 50; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var totalData = new byte[50 * 4];
        var buffer = new byte[8];
        int offset = 0;
        int bytesRead;

        while (
            (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0
        )
        {
            Array.Copy(buffer, 0, totalData, offset, bytesRead);
            offset += bytesRead;
        }

        Assert.Equal(50 * 4, offset);
        Assert.Equal(0, BitConverter.ToInt32(totalData, 0));
        Assert.Equal(49, BitConverter.ToInt32(totalData, 49 * 4));
    }

    [Fact]
    public async ValueTask TestAsyncReturnsZeroAtEndOfStream()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var buffer = new byte[4096];

        int bytesRead;
        while (
            (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0
        ) { }

        Assert.Equal(0, bytesRead);

        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask TestAsyncPosition()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 10; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        Assert.Equal(0, stream.Position);

        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(4, stream.Position);

        stream.StartRecording();
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(8, stream.Position);

        stream.Rewind();
        Assert.Equal(4, stream.Position);
    }

#if !LEGACY_DOTNET
    [Fact]
    public async ValueTask TestAsyncMemoryCancellationSupport()
    {
        var ms = new MemoryStream(new byte[10000]);
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var cts = new CancellationTokenSource();
        var buffer = new byte[4096];

        // Just verify that cancellation token can be passed without throwing
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(), cts.Token).ConfigureAwait(false);
        Assert.Equal(buffer.Length, bytesRead);
    }

    [Fact]
    public async ValueTask TestAsyncMemoryEmptyBuffer()
    {
        var ms = new MemoryStream();
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var buffer = Memory<byte>.Empty;
        int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask TestAsyncMemoryMultipleReads()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 50; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);

        var totalData = new byte[50 * 4];
        var buffer = new byte[8];
        int offset = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            Array.Copy(buffer, 0, totalData, offset, bytesRead);
            offset += bytesRead;
        }

        Assert.Equal(50 * 4, offset);
        Assert.Equal(0, BitConverter.ToInt32(totalData, 0));
        Assert.Equal(49, BitConverter.ToInt32(totalData, 49 * 4));
    }
#endif

    private static async Task<int> ReadInt32Async(Stream stream)
    {
        var buffer = new byte[4];
        var bytesRead = await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        if (bytesRead != 4)
        {
            throw new EndOfStreamException();
        }
        return buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
    }

#if !LEGACY_DOTNET
    private static async ValueTask<int> ReadInt32AsyncMemory(Stream stream)
    {
        var buffer = new byte[4];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
        if (bytesRead != 4)
        {
            throw new EndOfStreamException();
        }
        return buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
    }
#endif

    private static byte[] ReadAllSync(RewindableStream stream)
    {
        var result = new List<byte>();
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                result.Add(buffer[i]);
            }
        }

        return result.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(RewindableStream stream)
    {
        var result = new List<byte>();
        var buffer = new byte[4096];
        int bytesRead;

        while (
            (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0
        )
        {
            for (int i = 0; i < bytesRead; i++)
            {
                result.Add(buffer[i]);
            }
        }

        return result.ToArray();
    }

#if !LEGACY_DOTNET
    private static async ValueTask<byte[]> ReadAllAsyncMemory(RewindableStream stream)
    {
        var result = new List<byte>();
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                result.Add(buffer[i]);
            }
        }

        return result.ToArray();
    }
#endif

    [Fact]
    public async ValueTask TestStopRecordingAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));

        stream.StopRecording();

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));

        Assert.False(stream.IsRecording);
    }

    [Fact]
    public async ValueTask TestStopRecordingNoFurtherBufferingAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        var buffer = new byte[8];
        await stream.ReadAsync(buffer, 0, 8).ConfigureAwait(false);

        stream.StopRecording();

        await stream.ReadAsync(buffer, 0, 8).ConfigureAwait(false);
        Assert.Equal(BitConverter.GetBytes(1), buffer.Take(4).ToArray());
        Assert.Equal(BitConverter.GetBytes(2), buffer.Skip(4).Take(4).ToArray());

        int bytesRead = await stream.ReadAsync(buffer, 0, 8).ConfigureAwait(false);
        Assert.Equal(8, bytesRead);

        Assert.False(stream.IsRecording);
    }
}
