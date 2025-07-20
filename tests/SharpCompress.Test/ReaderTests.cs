using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

public abstract class ReaderTests : TestBase
{
    protected void Read(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions? options = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);

        options ??= new ReaderOptions() { BufferSize = 0x20000 }; //test larger buffer size (need test rather than eyeballing debug logs :P)

        options.LeaveStreamOpen = true;
        ReadImpl(testArchive, expectedCompression, options);

        options.LeaveStreamOpen = false;
        ReadImpl(testArchive, expectedCompression, options);
        VerifyFiles();
    }

    private void ReadImpl(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions options
    )
    {
        using var file = File.OpenRead(testArchive);
        using var protectedStream = SharpCompressStream.Create(
            new ForwardOnlyStream(file, options.BufferSize),
            leaveOpen: true,
            throwOnDispose: true,
            bufferSize: options.BufferSize
        );
        using var testStream = new TestStream(protectedStream);
        using (var reader = ReaderFactory.Open(testStream, options))
        {
            UseReader(reader, expectedCompression);
            protectedStream.ThrowOnDispose = false;
            Assert.False(testStream.IsDisposed, "{nameof(testStream)} prematurely closed");
        }

        // Boolean XOR -- If the stream should be left open (true), then the stream should not be diposed (false)
        // and if the stream should be closed (false), then the stream should be disposed (true)
        var message =
            $"{nameof(options.LeaveStreamOpen)} is set to '{options.LeaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
        Assert.True(options.LeaveStreamOpen != testStream.IsDisposed, message);
    }

    public void UseReader(IReader reader, CompressionType expectedCompression)
    {
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    protected void Iterate(
        string testArchive,
        string fileOrder,
        CompressionType expectedCompression,
        ReaderOptions? options = null
    )
    {
        if (!Environment.OSVersion.IsWindows())
        {
            fileOrder = fileOrder.Replace('\\', '/');
        }
        var expected = new Stack<string>(fileOrder.Split(' '));

        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using var file = File.OpenRead(testArchive);
        using var forward = new ForwardOnlyStream(file);
        using var reader = ReaderFactory.Open(forward, options);
        while (reader.MoveToNextEntry())
        {
            Assert.Equal(expectedCompression, reader.Entry.CompressionType);
            Assert.Equal(expected.Pop(), reader.Entry.Key);
        }
    }
}
