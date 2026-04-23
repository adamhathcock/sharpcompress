using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test;

public class WriterFactoryTests
{
    [Fact]
    public void OpenWriter_Stream_Throws_On_Unwritable_Stream()
    {
        using var unwritable = new TestStream(new MemoryStream(), true, false, true);

        Assert.Throws<ArgumentException>(() =>
            WriterFactory.OpenWriter(unwritable, ArchiveType.Zip, WriterOptions.ForZip())
        );
    }

    [Fact]
    public async ValueTask OpenAsyncWriter_Stream_Throws_On_Unwritable_Stream()
    {
        using var unwritable = new TestStream(new MemoryStream(), true, false, true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            WriterFactory
                .OpenAsyncWriter(unwritable, ArchiveType.Zip, WriterOptions.ForZip())
                .AsTask()
        );
    }
}
