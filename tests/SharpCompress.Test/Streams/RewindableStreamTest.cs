using System.IO;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams
{
    public class RewindableStreamTest
    {
        [Fact]
        public void TestRewind()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(1);
            bw.Write(2);
            bw.Write(3);
            bw.Write(4);
            bw.Write(5);
            bw.Write(6);
            bw.Write(7);
            bw.Flush();
            ms.Position = 0;
            RewindableStream stream = new RewindableStream(ms);
            stream.StartRecording();
            BinaryReader br = new BinaryReader(stream);
            Assert.Equal(1, br.ReadInt32());
            Assert.Equal(2, br.ReadInt32());
            Assert.Equal(3, br.ReadInt32());
            Assert.Equal(4, br.ReadInt32());
            stream.Rewind(true);
            stream.StartRecording();
            Assert.Equal(1, br.ReadInt32());
            Assert.Equal(2, br.ReadInt32());
            Assert.Equal(3, br.ReadInt32());
            Assert.Equal(4, br.ReadInt32());
            Assert.Equal(5, br.ReadInt32());
            Assert.Equal(6, br.ReadInt32());
            Assert.Equal(7, br.ReadInt32());
            stream.Rewind(true);
            stream.StartRecording();
            Assert.Equal(1, br.ReadInt32());
            Assert.Equal(2, br.ReadInt32());
            Assert.Equal(3, br.ReadInt32());
            Assert.Equal(4, br.ReadInt32());
        }

        [Fact]
        public void TestIncompleteRewind()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(1);
            bw.Write(2);
            bw.Write(3);
            bw.Write(4);
            bw.Write(5);
            bw.Write(6);
            bw.Write(7);
            bw.Flush();
            ms.Position = 0;
            RewindableStream stream = new RewindableStream(ms);
            stream.StartRecording();
            BinaryReader br = new BinaryReader(stream);
            Assert.Equal(1, br.ReadInt32());
            Assert.Equal(2, br.ReadInt32());
            Assert.Equal(3, br.ReadInt32());
            Assert.Equal(4, br.ReadInt32());
            stream.Rewind(true);
            Assert.Equal(1, br.ReadInt32());
            Assert.Equal(2, br.ReadInt32());
            stream.StartRecording();
            Assert.Equal(3, br.ReadInt32());
            Assert.Equal(4, br.ReadInt32());
            Assert.Equal(5, br.ReadInt32());
            stream.Rewind(true);
            Assert.Equal(3, br.ReadInt32());
            Assert.Equal(4, br.ReadInt32());
            Assert.Equal(5, br.ReadInt32());
            Assert.Equal(6, br.ReadInt32());
            Assert.Equal(7, br.ReadInt32());
        }
    }
}
