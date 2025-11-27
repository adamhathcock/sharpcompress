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
        var stream = new SharpCompressStream(ms, bufferSize: 0x10000);
        //stream.StartRecording();
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        //stream.Rewind(true);
        ((IStreamStack)stream).StackSeek(0);
        //stream.StartRecording();
        long pos = stream.Position;
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
        //stream.Rewind(true);
        //stream.StartRecording();
        ((IStreamStack)stream).StackSeek(pos);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
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
        var stream = new SharpCompressStream(ms, bufferSize: 0x10000);
        //stream.StartRecording();
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        ((IStreamStack)stream).StackSeek(0);
        //stream.Rewind(true);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        long pos = stream.Position;
        //stream.StartRecording();
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        ((IStreamStack)stream).StackSeek(pos);
        //stream.Rewind(true);
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestSmallBuffer()
    {
        var ms = new MemoryStream();
        var testData = new byte[100];
        for (byte i = 0; i < 100; i++)
        {
            testData[i] = i;
        }
        ms.Write(testData);
        ms.Position = 0;
        using var stream = new SharpCompressStream(ms, bufferSize: 64);
        var br = new BinaryReader(stream);
        stream.StackSeek(100);
        stream.StackSeek(10);
        Assert.Equal(10, br.ReadByte());
    }
}
