using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams
{
    public class RewindableStreamTest
    {
        [Fact]
        public async Task TestRewind()
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
            Assert.Equal(1, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(2, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(3, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(4, await stream.ReadInt32(CancellationToken.None));
            stream.Rewind(true);
            stream.StartRecording();
            Assert.Equal(1, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(2, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(3, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(4, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(5, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(6, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(7, await stream.ReadInt32(CancellationToken.None));
            stream.Rewind(true);
            stream.StartRecording();
            Assert.Equal(1, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(2, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(3, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(4, await stream.ReadInt32(CancellationToken.None));
        }

        [Fact]
        public async Task TestIncompleteRewind()
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
            Assert.Equal(1, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(2, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(3, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(4, await stream.ReadInt32(CancellationToken.None));
            stream.Rewind(true);
            Assert.Equal(1, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(2, await stream.ReadInt32(CancellationToken.None));
            stream.StartRecording();
            Assert.Equal(3, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(4, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(5, await stream.ReadInt32(CancellationToken.None));
            stream.Rewind(true);
            Assert.Equal(3, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(4, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(5, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(6, await stream.ReadInt32(CancellationToken.None));
            Assert.Equal(7, await stream.ReadInt32(CancellationToken.None));
        }
    }
}
