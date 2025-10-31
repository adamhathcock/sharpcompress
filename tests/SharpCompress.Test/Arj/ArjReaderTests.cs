using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

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
        public void Arj_Method4_Read() => Read("Arj.method4.arj");
    }
}
