using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
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
    }
}
