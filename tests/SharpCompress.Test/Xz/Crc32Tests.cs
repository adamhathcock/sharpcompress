using SharpCompress.Compressors.Xz;
using System;
using System.Text;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class Crc32Tests
    {
        private const string SimpleString = @"The quick brown fox jumps over the lazy dog.";
        private readonly byte[] SimpleBytes = Encoding.ASCII.GetBytes(SimpleString);
        private const string SimpleString2 = @"Life moves pretty fast. If you don't stop and look around once in a while, you could miss it.";
        private readonly byte[] SimpleBytes2 = Encoding.ASCII.GetBytes(SimpleString2);

        [Fact]
        public void ShortAsciiString()
        {
            var actual = Crc32.Compute(SimpleBytes);

            Assert.Equal((UInt32)0x519025e9, actual);
        }

        [Fact]
        public void ShortAsciiString2()
        {
            var actual = Crc32.Compute(SimpleBytes2);

            Assert.Equal((UInt32)0x6ee3ad88, actual);
        }

    }
}
