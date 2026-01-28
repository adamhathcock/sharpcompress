using System;
using System.IO;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class RewindableStreamTest
{
    [Fact]
    public void TestRewind()
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
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        stream.Rewind(true);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestIncompleteRewind()
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
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        stream.Rewind(true);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestRecording()
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
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        stream.Rewind(false);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
    }

    [Fact]
    public void TestRewindWithPartialBuffer()
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
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());

        // Create a buffer with the last 2 ints (12-16 bytes)
        var externalBuffer = new MemoryStream();
        externalBuffer.Write(BitConverter.GetBytes(3), 0, 4);
        externalBuffer.Write(BitConverter.GetBytes(4), 0, 4);
        externalBuffer.Write(BitConverter.GetBytes(5), 0, 4);
        externalBuffer.Write(BitConverter.GetBytes(6), 0, 4);
        externalBuffer.Position = 0;

        // Rewind by 12 bytes (3 ints worth)
        stream.Rewind(externalBuffer);
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestPosition()
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
        stream.Read(buffer, 0, 4);
        Assert.Equal(4, stream.Position);

        stream.StartRecording();
        stream.Read(buffer, 0, 4);
        Assert.Equal(8, stream.Position);

        stream.Rewind();
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void TestPositionSeek()
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
        var br = new BinaryReader(stream);

        Assert.Equal(0, br.ReadInt32());
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());

        stream.Position = 4;
        Assert.Equal(1, br.ReadInt32());
    }

    [Fact]
    public void TestDispose()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[4], 0, 4));
    }
}
