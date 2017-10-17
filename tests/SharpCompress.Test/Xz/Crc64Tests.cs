using SharpCompress.Compressors.Xz;
using System;
using System.Text;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class Crc64Tests
    {
        private const string SimpleString = @"The quick brown fox jumps over the lazy dog.";
        private readonly byte[] SimpleBytes = Encoding.ASCII.GetBytes(SimpleString);
        private const string SimpleString2 = @"Life moves pretty fast. If you don't stop and look around once in a while, you could miss it.";
        private readonly byte[] SimpleBytes2 = Encoding.ASCII.GetBytes(SimpleString2);

        [Fact]
        public void ShortAsciiString()
        {
            var actual = Crc64.Compute(SimpleBytes);

            Assert.Equal((UInt64)0x7E210EB1B03E5A1D, actual);
        }

        [Fact]
        public void ShortAsciiString2()
        {
            var actual = Crc64.Compute(SimpleBytes2);

            Assert.Equal((UInt64)0x416B4150508661EE, actual);
        }

    }
}
