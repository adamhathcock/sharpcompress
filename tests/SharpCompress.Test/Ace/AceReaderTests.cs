using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Ace;
using Xunit;

namespace SharpCompress.Test.Ace
{
    public class AceReaderTests : ReaderTests
    {
        public AceReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public void Ace_Uncompressed_Read() => Read("Ace.store.ace", CompressionType.None);

        [Fact]
        public void Ace_Encrypted_Read()
        {
            var exception = Assert.Throws<CryptographicException>(() => Read("Ace.encrypted.ace"));
        }

        [Theory]
        [InlineData("Ace.method1.ace", CompressionType.AceLZ77)]
        [InlineData("Ace.method1-solid.ace", CompressionType.AceLZ77)]
        [InlineData("Ace.method2.ace", CompressionType.AceLZ77)]
        [InlineData("Ace.method2-solid.ace", CompressionType.AceLZ77)]
        public void Ace_Unsupported_ShouldThrow(string fileName, CompressionType compressionType)
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                Read(fileName, compressionType)
            );
        }

        [Theory]
        [InlineData("Ace.store.largefile.ace", CompressionType.None)]
        public void Ace_LargeFileTest_Read(string fileName, CompressionType compressionType)
        {
            ReadForBufferBoundaryCheck(fileName, compressionType);
        }

        [Fact]
        public void Ace_Multi_Reader()
        {
            var exception = Assert.Throws<MultiVolumeExtractionException>(() =>
                DoMultiReader(
                    ["Ace.store.split.ace", "Ace.store.split.c01"],
                    streams => AceReader.Open(streams)
                )
            );
        }
    }
}
