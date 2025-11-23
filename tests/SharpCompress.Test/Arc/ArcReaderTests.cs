using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arc;
using Xunit;

namespace SharpCompress.Test.Arc
{
    public class ArcReaderTests : ReaderTests
    {
        public ArcReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public void Arc_Uncompressed_Read() => Read("Arc.uncompressed.arc", CompressionType.None);

        [Fact]
        public void Arc_Squeezed_Read() => Read("Arc.squeezed.arc");

        [Fact]
        public void Arc_Crunched_Read() => Read("Arc.crunched.arc");

        [Theory]
        [InlineData("Arc.crunched.largefile.arc", CompressionType.Crunched)]
        public void Arc_LargeFile_ShouldThrow(string fileName, CompressionType compressionType)
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                ReadForBufferBoundaryCheck(fileName, compressionType)
            );
        }

        [Theory]
        [InlineData("Arc.uncompressed.largefile.arc", CompressionType.None)]
        [InlineData("Arc.squeezed.largefile.arc", CompressionType.Squeezed)]
        public void Arc_LargeFileTest_Read(string fileName, CompressionType compressionType)
        {
            ReadForBufferBoundaryCheck(fileName, compressionType);
        }
    }
}
