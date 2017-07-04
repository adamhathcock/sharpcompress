using SharpCompress.Compressors.Xz;
using System.IO;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZStreamReaderTests : XZTestsBase
    {
        [Fact]
        public void CanReadStream()
        {
            XZStream xz = new XZStream(CompressedStream);
            using (var sr = new StreamReader(xz))
            {
                string uncompressed = sr.ReadToEnd();
                Assert.Equal(uncompressed, Original);
            }
        }
    }
}
