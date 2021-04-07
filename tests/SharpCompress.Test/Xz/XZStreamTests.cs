using SharpCompress.Compressors.Xz;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZStreamTests : XZTestsBase
    {
        [Fact]
        public async ValueTask CanReadEmptyStream()
        {
            XZStream xz = new XZStream(CompressedEmptyStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = await sr.ReadToEndAsync();
                Assert.Equal(OriginalEmpty, uncompressed);
            }
        }

        [Fact]
        public async ValueTask CanReadStream()
        {
            XZStream xz = new XZStream(CompressedStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = await sr.ReadToEndAsync();
                Assert.Equal(Original, uncompressed);
            }
        }

        [Fact]
        public async ValueTask CanReadIndexedStream()
        {
            XZStream xz = new XZStream(CompressedIndexedStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = await sr.ReadToEndAsync();
                Assert.Equal(OriginalIndexed, uncompressed);
            }
        }
    }
}
