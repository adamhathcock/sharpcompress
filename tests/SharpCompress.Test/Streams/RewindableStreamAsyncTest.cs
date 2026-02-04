using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
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

        stream.Rewind();
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
        var stream = new RewindableStream(new ForwardOnlyStream(ms));
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

    [Fact]
    public async ValueTask TestStopRecordingThenRewindAsync()
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
        bw.Write(8);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        // Read first 4 values (gets buffered)
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));

        // Stop recording
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // Rewind to start of buffer
        stream.Rewind(true);

        // Should be able to read from buffer again
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));

        // Continue reading remaining data from underlying stream
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(8, await ReadInt32Async(stream).ConfigureAwait(false));
    }

    [Fact]
    public async ValueTask TestMultipleRewindsAfterStopRecordingAsync()
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
        bw.Write(8);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        // Read first 4 values (gets buffered)
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));

        // Stop recording
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // First rewind - read all buffered data, then continue with underlying stream
        stream.Rewind();
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));

        // Second rewind - should still be able to read from buffer
        stream.Rewind();
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));

        // Third rewind - still works
        stream.Rewind();
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(8, await ReadInt32Async(stream).ConfigureAwait(false));
    }

    [Fact]
    public async ValueTask TestStopRecordingTwiceThrowsAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));

        // First StopRecording should succeed
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // Second StopRecording should throw
        Assert.Throws<InvalidOperationException>(() => stream.StopRecording());
    }

    [Fact]
    public async ValueTask TestReadMoreThanBufferSizeAfterRewindAsync()
    {
        // This test verifies the fix for the bug where reading more bytes than
        // are in the buffer after a rewind would only return the buffered bytes
        // instead of continuing to read from the underlying stream.
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 29 bytes (simulating Arc header)
        for (int i = 0; i < 29; i++)
        {
            bw.Write((byte)i);
        }

        // Write 5252 bytes (simulating Arc compressed data)
        for (int i = 0; i < 5252; i++)
        {
            bw.Write((byte)(i % 256));
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Simulate factory detection: record first 512 bytes
        stream.StartRecording();
        var probeBuffer = new byte[512];
        int probeRead = await stream.ReadAsync(probeBuffer, 0, 512).ConfigureAwait(false);
        Assert.Equal(512, probeRead);

        // Stop recording and rewind (simulates what ReaderFactory does)
        stream.Rewind(true);

        // Read header (29 bytes) - should come from buffer
        var headerBuffer = new byte[29];
        int headerRead = await stream.ReadAsync(headerBuffer, 0, 29).ConfigureAwait(false);
        Assert.Equal(29, headerRead);

        // Read compressed data (5252 bytes) - buffer has 483 bytes left,
        // but we need 5252 bytes. This should read all 5252 bytes, not just 483.
        var dataBuffer = new byte[5252];
        int dataRead = await stream.ReadAsync(dataBuffer, 0, 5252).ConfigureAwait(false);
        Assert.Equal(5252, dataRead);

        // Verify we read the correct data
        for (int i = 0; i < 5252; i++)
        {
            Assert.Equal((byte)(i % 256), dataBuffer[i]);
        }

        // Verify stream position is correct (29 + 5252 = 5281)
        Assert.Equal(5281, stream.Position);
    }

    [Fact]
    public async ValueTask TestReadExactlyBufferSizeAfterRewindAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 1024 bytes
        for (int i = 0; i < 1024; i++)
        {
            bw.Write((byte)(i % 256));
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Record first 512 bytes
        stream.StartRecording();
        var probeBuffer = new byte[512];
        await stream.ReadAsync(probeBuffer, 0, 512).ConfigureAwait(false);
        stream.Rewind(true);

        // Read exactly the buffer size (512 bytes)
        var buffer = new byte[512];
        int bytesRead = await stream.ReadAsync(buffer, 0, 512).ConfigureAwait(false);
        Assert.Equal(512, bytesRead);

        // Verify we read the correct data
        for (int i = 0; i < 512; i++)
        {
            Assert.Equal((byte)(i % 256), buffer[i]);
        }
    }

    [Fact]
    public async ValueTask TestReadLessThanBufferSizeAfterRewindAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 1024 bytes
        for (int i = 0; i < 1024; i++)
        {
            bw.Write((byte)(i % 256));
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Record first 512 bytes
        stream.StartRecording();
        var probeBuffer = new byte[512];
        await stream.ReadAsync(probeBuffer, 0, 512).ConfigureAwait(false);
        stream.Rewind(true);

        // Read less than buffer size (256 bytes)
        var buffer = new byte[256];
        int bytesRead = await stream.ReadAsync(buffer, 0, 256).ConfigureAwait(false);
        Assert.Equal(256, bytesRead);

        // Verify we read the correct data
        for (int i = 0; i < 256; i++)
        {
            Assert.Equal((byte)(i % 256), buffer[i]);
        }
    }

    [Fact]
    public async ValueTask TestMultipleReadsExceedingBufferAfterRewindAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 2048 bytes
        for (int i = 0; i < 2048; i++)
        {
            bw.Write((byte)(i % 256));
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Record first 512 bytes
        stream.StartRecording();
        var probeBuffer = new byte[512];
        await stream.ReadAsync(probeBuffer, 0, 512).ConfigureAwait(false);
        stream.Rewind(true);

        // Read in chunks that will exceed the buffer
        var buffer = new byte[800];

        // First read: 800 bytes (512 from buffer + 288 from underlying stream)
        int bytesRead1 = await stream.ReadAsync(buffer, 0, 800).ConfigureAwait(false);
        Assert.Equal(800, bytesRead1);

        // Second read: 800 bytes (all from underlying stream)
        int bytesRead2 = await stream.ReadAsync(buffer, 0, 800).ConfigureAwait(false);
        Assert.Equal(800, bytesRead2);

        // Third read: remaining 448 bytes
        int bytesRead3 = await stream.ReadAsync(buffer, 0, 800).ConfigureAwait(false);
        Assert.Equal(448, bytesRead3);

        // Verify stream position
        Assert.Equal(2048, stream.Position);
    }

    [Fact]
    public async ValueTask TestReadPartiallyFromBufferThenUnderlyingStreamAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 1000 bytes with specific pattern
        for (int i = 0; i < 1000; i++)
        {
            bw.Write((byte)i);
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Record first 100 bytes
        stream.StartRecording();
        var probeBuffer = new byte[100];
        await stream.ReadAsync(probeBuffer, 0, 100).ConfigureAwait(false);
        stream.Rewind(true);

        // Read 50 bytes (from buffer)
        var buffer1 = new byte[50];
        int read1 = await stream.ReadAsync(buffer1, 0, 50).ConfigureAwait(false);
        Assert.Equal(50, read1);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal((byte)i, buffer1[i]);
        }

        // Read 150 bytes (50 from buffer + 100 from underlying stream)
        var buffer2 = new byte[150];
        int read2 = await stream.ReadAsync(buffer2, 0, 150).ConfigureAwait(false);
        Assert.Equal(150, read2);
        for (int i = 0; i < 150; i++)
        {
            Assert.Equal((byte)(i + 50), buffer2[i]);
        }

        // Verify position
        Assert.Equal(200, stream.Position);
    }

#if !LEGACY_DOTNET
    [Fact]
    public async ValueTask TestReadMoreThanBufferSizeAfterRewindMemoryAsync()
    {
        // Same as TestReadMoreThanBufferSizeAfterRewindAsync but using Memory<byte>
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 29 bytes (simulating Arc header)
        for (int i = 0; i < 29; i++)
        {
            bw.Write((byte)i);
        }

        // Write 5252 bytes (simulating Arc compressed data)
        for (int i = 0; i < 5252; i++)
        {
            bw.Write((byte)(i % 256));
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Simulate factory detection: record first 512 bytes
        stream.StartRecording();
        var probeBuffer = new byte[512];
        int probeRead = await stream.ReadAsync(probeBuffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(512, probeRead);

        // Stop recording and rewind (simulates what ReaderFactory does)
        stream.Rewind(true);

        // Read header (29 bytes) - should come from buffer
        var headerBuffer = new byte[29];
        int headerRead = await stream.ReadAsync(headerBuffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(29, headerRead);

        // Read compressed data (5252 bytes) - buffer has 483 bytes left,
        // but we need 5252 bytes. This should read all 5252 bytes, not just 483.
        var dataBuffer = new byte[5252];
        int dataRead = await stream.ReadAsync(dataBuffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(5252, dataRead);

        // Verify we read the correct data
        for (int i = 0; i < 5252; i++)
        {
            Assert.Equal((byte)(i % 256), dataBuffer[i]);
        }

        // Verify stream position is correct (29 + 5252 = 5281)
        Assert.Equal(5281, stream.Position);
    }

    [Fact]
    public async ValueTask TestMultipleReadsExceedingBufferAfterRewindMemoryAsync()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

        // Write 2048 bytes
        for (int i = 0; i < 2048; i++)
        {
            bw.Write((byte)(i % 256));
        }

        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));

        // Record first 512 bytes
        stream.StartRecording();
        var probeBuffer = new byte[512];
        await stream.ReadAsync(probeBuffer.AsMemory()).ConfigureAwait(false);
        stream.Rewind(true);

        // Read in chunks that will exceed the buffer
        var buffer = new byte[800];

        // First read: 800 bytes (512 from buffer + 288 from underlying stream)
        int bytesRead1 = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(800, bytesRead1);

        // Second read: 800 bytes (all from underlying stream)
        int bytesRead2 = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(800, bytesRead2);

        // Third read: remaining 448 bytes
        int bytesRead3 = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(448, bytesRead3);

        // Verify stream position
        Assert.Equal(2048, stream.Position);
    }
#endif
}
