using System;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test
{
    public class Rar5ReaderTests : ReaderTests
    {
     
        [Fact]
        public void Rar_Reader()
        {
            Assert.Throws<InvalidOperationException>(() => Read("Rar5.rar", CompressionType.Rar));
        }
    }
}
