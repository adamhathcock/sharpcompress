using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arj;
using Xunit;
using Xunit.Sdk;

namespace SharpCompress.Test.Arj
{
    public class ArjReaderTests : ReaderTests
    {
        public ArjReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public void Arj_Uncompressed_Read() => Read("Arj.store.arj", CompressionType.None);

        [Fact]
        public void Arj_Method1_Read() => Read("Arj.method1.arj");

        [Fact]
        public void Arj_Method2_Read() => Read("Arj.method2.arj");

        [Fact]
        public void Arj_Method3_Read() => Read("Arj.method3.arj");

        [Fact]
        public void Arj_Method4_Read() => Read("Arj.method4.arj");

        [Fact]
        public void Arj_Encrypted_Read()
        {
            var exception = Assert.Throws<CryptographicException>(() => Read("Arj.encrypted.arj"));
        }

        [Fact]
        public void Arj_Multi_Reader()
        {
            var exception = Assert.Throws<MultiVolumeExtractionException>(() =>
                DoMultiReader(
                    [
                        "Arj.store.split.arj",
                        "Arj.store.split.a01",
                        "Arj.store.split.a02",
                        "Arj.store.split.a03",
                        "Arj.store.split.a04",
                        "Arj.store.split.a05",
                    ],
                    streams => ArjReader.Open(streams)
                )
            );
        }

        [Theory]
        [InlineData("Arj.method1.largefile.arj", CompressionType.ArjLZ77)]
        [InlineData("Arj.method2.largefile.arj", CompressionType.ArjLZ77)]
        [InlineData("Arj.method3.largefile.arj", CompressionType.ArjLZ77)]
        public void Arj_LargeFile_ShouldThrow(string fileName, CompressionType compressionType)
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                ReadForBufferBoundaryCheck(fileName, compressionType)
            );
        }

        [Theory]
        [InlineData("Arj.store.largefile.arj", CompressionType.None)]
        [InlineData("Arj.method4.largefile.arj", CompressionType.ArjLZ77)]
        public void Arj_LargeFileTest_Read(string fileName, CompressionType compressionType)
        {
            ReadForBufferBoundaryCheck(fileName, compressionType);
        }
    }
}
