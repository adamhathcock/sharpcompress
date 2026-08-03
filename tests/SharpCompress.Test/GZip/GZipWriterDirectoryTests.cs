using System;
using System.IO;
using System.Threading.Tasks;
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

    [Fact]
    public async ValueTask GZipWriter_WriteDirectoryAsync_ThrowsNotSupportedException()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new GZipWriter(memoryStream, new GZipWriterOptions());

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await writer.WriteDirectoryAsync("test-dir", DateTime.Now)
        );
    }
}
