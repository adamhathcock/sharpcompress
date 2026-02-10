using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

public abstract class ReaderTests : TestBase
{
    protected void Read(string testArchive, ReaderOptions? options = null)
    {
        ReadCore(testArchive, options, ReadImpl);
    }

    protected void Read(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions? options = null
    )
    {
        ReadCore(testArchive, options, (path, opts) => ReadImpl(path, expectedCompression, opts));
    }

    private void ReadCore(
        string testArchive,
        ReaderOptions? options,
        Action<string, ReaderOptions> readImpl
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        options ??= new ReaderOptions { BufferSize = 0x20000 };

        var optionsWithStreamOpen = options with { LeaveStreamOpen = true };
        readImpl(testArchive, optionsWithStreamOpen);

        var optionsWithStreamClosed = options with { LeaveStreamOpen = false };
        readImpl(testArchive, optionsWithStreamClosed);

        VerifyFiles();
    }

    private void ReadImpl(string testArchive, ReaderOptions options)
    {
        ReadImplCore(testArchive, options, UseReader);
    }

    private void ReadImpl(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions options
    )
    {
        ReadImplCore(testArchive, options, r => UseReader(r, expectedCompression));
    }

    private void ReadImplCore(string testArchive, ReaderOptions options, Action<IReader> useReader)
    {
        using var file = File.OpenRead(testArchive);
        using var protectedStream = SharpCompressStream.CreateNonDisposing(
            new ForwardOnlyStream(file, options.BufferSize)
        );
        using var testStream = new TestStream(protectedStream);
        using (var reader = ReaderFactory.OpenReader(testStream, options))
        {
            useReader(reader);
            Assert.False(testStream.IsDisposed, $"{nameof(testStream)} prematurely closed");
        }

        var message =
            $"{nameof(options.LeaveStreamOpen)} is set to '{options.LeaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
        Assert.True(options.LeaveStreamOpen != testStream.IsDisposed, message);
    }

    protected void UseReader(IReader reader, CompressionType expectedCompression)
    {
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                reader.WriteEntryToDirectory(SCRATCH_FILES_PATH);
            }
        }
    }

    private void UseReader(IReader reader)
    {
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(SCRATCH_FILES_PATH);
            }
        }
    }

    protected async Task AssertArchiveAsync<T>(
        string testArchive,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        var factory = new TarFactory();
        factory.IsArchive(new FileInfo(testArchive).OpenRead()).Should().BeTrue();
        (
            await factory.IsArchiveAsync(
                new FileInfo(testArchive).OpenRead(),
                cancellationToken: cancellationToken
            )
        )
            .Should()
            .BeTrue();
    }

    protected async Task ReadAsync(
        string testArchive,
        CompressionType? expectedCompression = null,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);

        options ??= new ReaderOptions() { BufferSize = 0x20000 };

        var optionsWithStreamOpen = options with { LeaveStreamOpen = true };
        await ReadImplAsync(
            testArchive,
            expectedCompression,
            optionsWithStreamOpen,
            cancellationToken
        );

        var optionsWithStreamClosed = options with { LeaveStreamOpen = false };
        await ReadImplAsync(
            testArchive,
            expectedCompression,
            optionsWithStreamClosed,
            cancellationToken
        );

        VerifyFiles();
    }

    private async ValueTask ReadImplAsync(
        string testArchive,
        CompressionType? expectedCompression,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        using var file = File.OpenRead(testArchive);

#if !LEGACY_DOTNET
        await using var protectedStream = SharpCompressStream.CreateNonDisposing(
            new ForwardOnlyStream(file, options.BufferSize)
        );
        await using var testStream = new TestStream(protectedStream);
#else

        using var protectedStream = SharpCompressStream.CreateNonDisposing(
            new ForwardOnlyStream(file, options.BufferSize)
        );
        using var testStream = new TestStream(protectedStream);
#endif
        await using (
            var reader = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(testStream),
                options,
                cancellationToken
            )
        )
        {
            await UseReaderAsync(reader, expectedCompression, cancellationToken);
            Assert.False(testStream.IsDisposed, $"{nameof(testStream)} prematurely closed");
        }

        var message =
            $"{nameof(options.LeaveStreamOpen)} is set to '{options.LeaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
        Assert.True(options.LeaveStreamOpen != testStream.IsDisposed, message);
    }

    public async ValueTask UseReaderAsync(
        IAsyncReader reader,
        CompressionType? expectedCompression,
        CancellationToken cancellationToken = default
    )
    {
        while (await reader.MoveToNextEntryAsync(cancellationToken))
        {
            if (!reader.Entry.IsDirectory)
            {
                if (expectedCompression.HasValue)
                {
                    Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                }

                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH, cancellationToken);
            }
        }
    }

    protected void ReadForBufferBoundaryCheck(string fileName, CompressionType compressionType)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, fileName));
        using var reader = ReaderFactory.OpenReader(
            stream,
            new ReaderOptions { LookForHeader = true }
        );

        while (reader.MoveToNextEntry())
        {
            Assert.Equal(compressionType, reader.Entry.CompressionType);

            reader.WriteEntryToDirectory(SCRATCH_FILES_PATH);
        }

        CompareFilesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "alice29.txt"),
            Path.Combine(MISC_TEST_FILES_PATH, "alice29.txt")
        );
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
        using var reader = ReaderFactory.OpenReader(forward, options);
        while (reader.MoveToNextEntry())
        {
            Assert.Equal(expectedCompression, reader.Entry.CompressionType);
            Assert.Equal(expected.Pop(), reader.Entry.Key);
        }
    }

    protected void DoMultiReader(
        string[] archives,
        Func<IEnumerable<Stream>, IReader> readerFactory
    )
    {
        using var reader = readerFactory(
            archives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s)).Select(File.OpenRead)
        );

        while (reader.MoveToNextEntry())
        {
            reader.WriteEntryToDirectory(SCRATCH_FILES_PATH);
        }

        VerifyFiles();
    }
}
