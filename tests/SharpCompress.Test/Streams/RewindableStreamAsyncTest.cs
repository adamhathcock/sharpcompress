using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class RewindableStreamAsyncTest
{
    [Fact]
    public async Task TestRewindAsync()
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

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));

        ((IStreamStack)stream).StackSeek(0);
        long pos = stream.Position;
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));

        ((IStreamStack)stream).StackSeek(pos);
        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
    }

    [Fact]
    public async Task TestIncompleteRewindAsync()
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

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        ((IStreamStack)stream).StackSeek(0);

        Assert.Equal(1, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(2, await ReadInt32Async(stream).ConfigureAwait(false));
        long pos = stream.Position;

        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        ((IStreamStack)stream).StackSeek(pos);

        Assert.Equal(3, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(4, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(5, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(6, await ReadInt32Async(stream).ConfigureAwait(false));
        Assert.Equal(7, await ReadInt32Async(stream).ConfigureAwait(false));
    }

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
}
