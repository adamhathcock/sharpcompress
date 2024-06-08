using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using Xunit;

namespace SharpCompress.Test.BZip2;

public class BZip2ReaderTests : ReaderTests
{
    [Fact]
    public void BZip2_Reader_Factory()
    {
        Stream stream = new MemoryStream(
            new byte[] { 0x42, 0x5a, 0x68, 0x34, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59, 0x35 }
        );
        Assert.Throws(typeof(InvalidOperationException), () => ReaderFactory.Open(stream));
    }
}
