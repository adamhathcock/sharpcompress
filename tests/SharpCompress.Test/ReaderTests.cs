using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
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

        options.LeaveStreamOpen = true;
        readImpl(testArchive, options);

        options.LeaveStreamOpen = false;
        readImpl(testArchive, options);

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
        using var protectedStream = SharpCompressStream.Create(
            new ForwardOnlyStream(file, options.BufferSize),
            leaveOpen: true,
            throwOnDispose: true,
            bufferSize: options.BufferSize
        );
        using var testStream = new TestStream(protectedStream);
        using (var reader = ReaderFactory.Open(testStream, options))
        {
            useReader(reader);
            protectedStream.ThrowOnDispose = false;
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
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    private void UseReader(IReader reader)
    {
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }

    protected async Task ReadAsync(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);

        options ??= new ReaderOptions() { BufferSize = 0x20000 };

        options.LeaveStreamOpen = true;
        await ReadImplAsync(testArchive, expectedCompression, options, cancellationToken);

        options.LeaveStreamOpen = false;
        await ReadImplAsync(testArchive, expectedCompression, options, cancellationToken);
        VerifyFiles();
    }

    private async Task ReadImplAsync(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions options,
        CancellationToken cancellationToken = default
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
            await UseReaderAsync(reader, expectedCompression, cancellationToken);
            protectedStream.ThrowOnDispose = false;
            Assert.False(testStream.IsDisposed, $"{nameof(testStream)} prematurely closed");
        }

        var message =
            $"{nameof(options.LeaveStreamOpen)} is set to '{options.LeaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
        Assert.True(options.LeaveStreamOpen != testStream.IsDisposed, message);
    }

    public async Task UseReaderAsync(
        IReader reader,
        CompressionType expectedCompression,
        CancellationToken cancellationToken = default
    )
    {
        while (await reader.MoveToNextEntryAsync(cancellationToken))
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
                    cancellationToken
                );
            }
        }
    }

    protected void ReadForBufferBoundaryCheck(string fileName, CompressionType compressionType)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, fileName));
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LookForHeader = true });

        while (reader.MoveToNextEntry())
        {
            Assert.Equal(compressionType, reader.Entry.CompressionType);

            reader.WriteEntryToDirectory(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
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
        using var reader = ReaderFactory.Open(forward, options);
        while (reader.MoveToNextEntry())
        {
            Assert.Equal(expectedCompression, reader.Entry.CompressionType);
            Assert.Equal(expected.Pop(), reader.Entry.Key);
        }
    }

    protected void DoMultiReader(
        string[] archives,
        Func<IEnumerable<Stream>, IDisposable> readerFactory
    )
    {
        using var reader = readerFactory(
            archives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s)).Select(File.OpenRead)
        );

        dynamic dynReader = reader;

        while (dynReader.MoveToNextEntry())
        {
            dynReader.WriteEntryToDirectory(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }

        VerifyFiles();
    }
}
