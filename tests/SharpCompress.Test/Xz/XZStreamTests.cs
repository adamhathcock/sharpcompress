using SharpCompress.Compressors.Xz;
using System.IO;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZStreamTests : XZTestsBase
    {
        [Fact]
        public void CanReadEmptyStream()
        {
            XZStream xz = new XZStream(CompressedEmptyStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = sr.ReadToEnd();
                Assert.Equal(OriginalEmpty, uncompressed);
            }
        }

        [Fact]
        public void CanReadStream()
        {
            XZStream xz = new XZStream(CompressedStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = sr.ReadToEnd();
                Assert.Equal(Original, uncompressed);
            }
        }

        [Fact]
        public void CanReadIndexedStream()
        {
            XZStream xz = new XZStream(CompressedIndexedStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = sr.ReadToEnd();
                Assert.Equal(OriginalIndexed, uncompressed);
            }
        }
    }
}
