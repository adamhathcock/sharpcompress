using System.IO;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test
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
            Assert.Equal(br.ReadInt32(), 1);
            Assert.Equal(br.ReadInt32(), 2);
            Assert.Equal(br.ReadInt32(), 3);
            Assert.Equal(br.ReadInt32(), 4);
            stream.Rewind(true);
            stream.StartRecording();
            Assert.Equal(br.ReadInt32(), 1);
            Assert.Equal(br.ReadInt32(), 2);
            Assert.Equal(br.ReadInt32(), 3);
            Assert.Equal(br.ReadInt32(), 4);
            Assert.Equal(br.ReadInt32(), 5);
            Assert.Equal(br.ReadInt32(), 6);
            Assert.Equal(br.ReadInt32(), 7);
            stream.Rewind(true);
            stream.StartRecording();
            Assert.Equal(br.ReadInt32(), 1);
            Assert.Equal(br.ReadInt32(), 2);
            Assert.Equal(br.ReadInt32(), 3);
            Assert.Equal(br.ReadInt32(), 4);
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
            Assert.Equal(br.ReadInt32(), 1);
            Assert.Equal(br.ReadInt32(), 2);
            Assert.Equal(br.ReadInt32(), 3);
            Assert.Equal(br.ReadInt32(), 4);
            stream.Rewind(true);
            Assert.Equal(br.ReadInt32(), 1);
            Assert.Equal(br.ReadInt32(), 2);
            stream.StartRecording();
            Assert.Equal(br.ReadInt32(), 3);
            Assert.Equal(br.ReadInt32(), 4);
            Assert.Equal(br.ReadInt32(), 5);
            stream.Rewind(true);
            Assert.Equal(br.ReadInt32(), 3);
            Assert.Equal(br.ReadInt32(), 4);
            Assert.Equal(br.ReadInt32(), 5);
            Assert.Equal(br.ReadInt32(), 6);
            Assert.Equal(br.ReadInt32(), 7);
        }
    }
}
