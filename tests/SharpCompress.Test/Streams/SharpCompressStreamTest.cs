using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamTests
{
    private static void createData(MemoryStream ms)
    {
        using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            //write offset every 4 bytes - easy to test position
            for (int i = 0; i < ms.Length; i += 4)
            {
                bw.Write(i);
            }
        }
        ms.Position = 0;
    }

    [Fact]
    public void BufferReadTest()
    {
        byte[] data = new byte[0x100000];
        byte[] test = new byte[0x1000];
        using (MemoryStream ms = new MemoryStream(data))
        {
            createData(ms);

            using (SharpCompressStream scs = new SharpCompressStream(ms, true, false, 0x10000))
            {
                IStreamStack stack = (IStreamStack)scs;

                scs.Seek(0x1000, SeekOrigin.Begin);
                Assert.Equal(0x1000, scs.Position); //position in the SharpCompressionStream (with 0xf000 remaining in the buffer)
                Assert.Equal(0x1000, ms.Position); //initial seek + full buffer read

                scs.Read(test, 0, 0x1000); //read bytes 0x1000 to 0x2000
                Assert.Equal(0x2000, scs.Position); //stream has correct position
                Assert.True(data.Skip(0x1000).Take(0x1000).SequenceEqual(test.Take(0x1000))); //is the data correct
                Assert.Equal(0x11000, ms.Position); //seek plus read bytes

                scs.Seek(0x500, SeekOrigin.Begin); //seek before the buffer start
                scs.Read(test, 0, 0x1000); //read bytes 0x500 to 0x1500
                Assert.Equal(0x1500, scs.Position); //stream has correct position
                Assert.True(data.Skip(0x500).Take(0x1000).SequenceEqual(test.Take(0x1000))); //is the data correct
                Assert.Equal(0x10500, ms.Position); //seek plus read bytes
            }
        }
    }

    [Fact]
    public void BufferReadAndSeekTest()
    {
        byte[] data = new byte[0x100000];
        byte[] test = new byte[0x1000];
        using (MemoryStream ms = new MemoryStream(data))
        {
            createData(ms);

            using (SharpCompressStream scs = new SharpCompressStream(ms, true, false, 0x10000))
            {
                IStreamStack stack = (IStreamStack)scs;

                scs.Read(test, 0, 0x1000); //read bytes 0 to 0x1000
                Assert.True(data.Take(0x1000).SequenceEqual(test.Take(0x1000))); //is the data correct
                Assert.Equal(0x1000, scs.Position); //stream has correct position
                Assert.Equal(0x10000, ms.Position); //moved the base stream on by the size of the buffer not what was requested

                scs.Read(test, 0, 0x1000); //read bytes 0x1000 to 0x2000
                Assert.Equal(0x2000, scs.Position); //stream has correct position
                Assert.True(data.Skip(0x1000).Take(0x1000).SequenceEqual(test.Take(0x1000))); //is the data correct
                Assert.Equal(0x10000, ms.Position); //the base stream has not moved

                //rewind the buffer
                stack.Rewind(0x1000); //rewind buffer back by 0x1000 bytes

                //repeat the previous test
                scs.Read(test, 0, 0x1000); //read bytes 0x1000 to 0x2000
                Assert.Equal(0x2000, scs.Position); //stream has correct position
                Assert.True(data.Skip(0x1000).Take(0x1000).SequenceEqual(test.Take(0x1000))); //is the data correct
                Assert.Equal(0x10000, ms.Position); //the base stream has not moved
            }
        }
    }
}
