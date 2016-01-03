using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Reader.Rar;
using Xunit;

namespace SharpCompress.Test
{
    public class Rar5ReaderTests : ReaderTests
    {
     
        [Fact]
        public void Rar_Reader()
        {
            Read("Rar5.rar", CompressionType.Rar);
        }
    }
}
