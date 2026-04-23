using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

public class ReaderFactoryTests
{
    [Fact]
    public void OpenReader_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        Assert.Throws<ArgumentException>(() => ReaderFactory.OpenReader(unreadable));
    }

    [Fact]
    public async ValueTask OpenAsyncReader_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderFactory.OpenAsyncReader(unreadable).AsTask()
        );
    }

    [Fact]
    public void RarReader_StreamCollection_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);
        using var readable = new MemoryStream();

        Assert.Throws<ArgumentException>(() =>
            RarReader.OpenReader([unreadable, readable]).MoveToNextEntry()
        );
    }
}
