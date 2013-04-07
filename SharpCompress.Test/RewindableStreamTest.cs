using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.IO;

namespace SharpCompress.Test
{
    [TestClass]
    public class RewindableStreamTest
    {
        [TestMethod]
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
            Assert.AreEqual(br.ReadInt32(), 1);
            Assert.AreEqual(br.ReadInt32(), 2);
            Assert.AreEqual(br.ReadInt32(), 3);
            Assert.AreEqual(br.ReadInt32(), 4);
            stream.Rewind(true);
            stream.StartRecording();
            Assert.AreEqual(br.ReadInt32(), 1);
            Assert.AreEqual(br.ReadInt32(), 2);
            Assert.AreEqual(br.ReadInt32(), 3);
            Assert.AreEqual(br.ReadInt32(), 4);
            Assert.AreEqual(br.ReadInt32(), 5);
            Assert.AreEqual(br.ReadInt32(), 6);
            Assert.AreEqual(br.ReadInt32(), 7);
            stream.Rewind(true);
            stream.StartRecording();
            Assert.AreEqual(br.ReadInt32(), 1);
            Assert.AreEqual(br.ReadInt32(), 2);
            Assert.AreEqual(br.ReadInt32(), 3);
            Assert.AreEqual(br.ReadInt32(), 4);
        }

        [TestMethod]
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
            Assert.AreEqual(br.ReadInt32(), 1);
            Assert.AreEqual(br.ReadInt32(), 2);
            Assert.AreEqual(br.ReadInt32(), 3);
            Assert.AreEqual(br.ReadInt32(), 4);
            stream.Rewind(true);
            Assert.AreEqual(br.ReadInt32(), 1);
            Assert.AreEqual(br.ReadInt32(), 2);
            stream.StartRecording();
            Assert.AreEqual(br.ReadInt32(), 3);
            Assert.AreEqual(br.ReadInt32(), 4);
            Assert.AreEqual(br.ReadInt32(), 5);
            stream.Rewind(true);
            Assert.AreEqual(br.ReadInt32(), 3);
            Assert.AreEqual(br.ReadInt32(), 4);
            Assert.AreEqual(br.ReadInt32(), 5);
            Assert.AreEqual(br.ReadInt32(), 6);
            Assert.AreEqual(br.ReadInt32(), 7);
        }
    }
}
