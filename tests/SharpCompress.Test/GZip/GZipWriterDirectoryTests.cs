using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipWriterDirectoryTests : TestBase
{
    [Fact]
    public void GZipWriter_WriteDirectory_ThrowsNotSupportedException()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new GZipWriter(memoryStream, new GZipWriterOptions());

        Assert.Throws<NotSupportedException>(() => writer.WriteDirectory("test-dir", DateTime.Now));
    }
}
