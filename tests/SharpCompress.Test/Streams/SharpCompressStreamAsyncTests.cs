using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamAsyncTests
{
    private static void CreateData(MemoryStream ms)
    {
        using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            // write offset every 4 bytes - easy to test position
            for (int i = 0; i < ms.Length; i += 4)
            {
                bw.Write(i);
            }
        }
        ms.Position = 0;
    }

    [Fact]
    public async Task BufferReadAsyncTest()
    {
        byte[] data = new byte[0x100000];
        byte[] test = new byte[0x1000];
        using (MemoryStream ms = new MemoryStream(data))
        {
            CreateData(ms);

            using (SharpCompressStream scs = new SharpCompressStream(ms, true, false, 0x10000))
            {
                scs.Seek(0x1000, SeekOrigin.Begin);
                Assert.Equal(0x1000, scs.Position); // position in the SharpCompressionStream
                Assert.Equal(0x1000, ms.Position); // initial seek + full buffer read

                await scs.ReadAsync(test, 0, test.Length); // read bytes 0x1000 to 0x2000
                Assert.Equal(0x2000, scs.Position); // stream has correct position
                Assert.True(data.Skip(test.Length).Take(test.Length).SequenceEqual(test)); // is the data correct
                Assert.Equal(0x11000, ms.Position); // seek plus read bytes

                scs.Seek(0x500, SeekOrigin.Begin); // seek before the buffer start
                await scs.ReadAsync(test, 0, test.Length); // read bytes 0x500 to 0x1500
                Assert.Equal(0x1500, scs.Position); // stream has correct position
                Assert.True(data.Skip(0x500).Take(test.Length).SequenceEqual(test)); // is the data correct
                Assert.Equal(0x10500, ms.Position); // seek plus read bytes
            }
        }
    }

    [Fact]
    public async Task BufferReadAndSeekAsyncTest()
    {
        byte[] data = new byte[0x100000];
        byte[] test = new byte[0x1000];
        using (MemoryStream ms = new MemoryStream(data))
        {
            CreateData(ms);

            using (SharpCompressStream scs = new SharpCompressStream(ms, true, false, 0x10000))
            {
                IStreamStack stack = (IStreamStack)scs;

                await scs.ReadAsync(test, 0, test.Length); // read bytes 0 to 0x1000
                Assert.True(data.Take(test.Length).SequenceEqual(test)); // is the data correct
                Assert.Equal(0x1000, scs.Position); // stream has correct position
                Assert.Equal(0x10000, ms.Position); // moved the base stream on by buffer size

                await scs.ReadAsync(test, 0, test.Length); // read bytes 0x1000 to 0x2000
                Assert.Equal(0x2000, scs.Position); // stream has correct position
                Assert.True(data.Skip(test.Length).Take(test.Length).SequenceEqual(test)); // is the data correct
                Assert.Equal(0x10000, ms.Position); // the base stream has not moved

                // rewind the buffer
                stack.Rewind(0x1000); // rewind buffer back by 0x1000 bytes

                // repeat the previous test
                await scs.ReadAsync(test, 0, test.Length); // read bytes 0x1000 to 0x2000
                Assert.Equal(0x2000, scs.Position); // stream has correct position
                Assert.True(data.Skip(test.Length).Take(test.Length).SequenceEqual(test)); // is the data correct
                Assert.Equal(0x10000, ms.Position); // the base stream has not moved
            }
        }
    }

    [Fact]
    public async Task MultipleAsyncReadsTest()
    {
        byte[] data = new byte[0x100000];
        byte[] test1 = new byte[0x800];
        byte[] test2 = new byte[0x800];
        using (MemoryStream ms = new MemoryStream(data))
        {
            CreateData(ms);

            using (SharpCompressStream scs = new SharpCompressStream(ms, true, false, 0x10000))
            {
                // Read first chunk
                await scs.ReadAsync(test1, 0, test1.Length);
                Assert.Equal(0x800, scs.Position);
                Assert.True(data.Take(test1.Length).SequenceEqual(test1)); // first read is correct

                // Read second chunk
                await scs.ReadAsync(test2, 0, test2.Length);
                Assert.Equal(0x1000, scs.Position);
                Assert.True(data.Skip(test1.Length).Take(test2.Length).SequenceEqual(test2)); // second read is correct
            }
        }
    }

    [Fact]
    public async Task LargeBufferAsyncReadTest()
    {
        byte[] data = new byte[0x200000];
        byte[] test = new byte[0x8000];
        using (MemoryStream ms = new MemoryStream(data))
        {
            CreateData(ms);

            using (SharpCompressStream scs = new SharpCompressStream(ms, true, false, 0x10000))
            {
                for (int i = 0; i < 10; i++)
                {
                    await scs.ReadAsync(test, 0, test.Length);
                    long expectedPosition = (long)(i + 1) * test.Length;
                    Assert.Equal(expectedPosition, scs.Position);
                    Assert.True(data.Skip(i * test.Length).Take(test.Length).SequenceEqual(test));
                }
            }
        }
    }
}
