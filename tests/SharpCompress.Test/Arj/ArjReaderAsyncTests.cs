using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arj;
using SharpCompress.Test.Mocks;
using Xunit;
using Xunit.Sdk;

namespace SharpCompress.Test.Arj;

public class ArjReaderAsyncTests : ReaderTests
{
    public ArjReaderAsyncTests()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
    }

    [Fact]
    public async ValueTask Arj_Uncompressed_Read_Async() =>
        await ReadAsync("Arj.store.arj", CompressionType.None);

    [Fact]
    public async ValueTask Arj_Method1_Read_Async() => await ReadAsync("Arj.method1.arj");

    [Fact]
    public async ValueTask Arj_Method2_Read_Async() => await ReadAsync("Arj.method2.arj");

    [Fact]
    public async ValueTask Arj_Method3_Read_Async() => await ReadAsync("Arj.method3.arj");

    [Fact]
    public async ValueTask Arj_Method4_Read_Async() => await ReadAsync("Arj.method4.arj");

    [Fact]
    public async ValueTask Arj_Encrypted_Read_Async()
    {
        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            ReadAsync("Arj.encrypted.arj")
        );
    }

    [Fact]
    public async ValueTask Arj_Multi_Reader_Async()
    {
        var exception = await Assert.ThrowsAsync<MultiVolumeExtractionException>(() =>
            DoMultiReaderAsync(
                [
                    "Arj.store.split.arj",
                    "Arj.store.split.a01",
                    "Arj.store.split.a02",
                    "Arj.store.split.a03",
                    "Arj.store.split.a04",
                    "Arj.store.split.a05",
                ],
                streams => ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(streams.First()))
            )
        );
    }

    [Theory]
    [InlineData("Arj.store.largefile.arj", CompressionType.None)]
    [InlineData("Arj.method1.largefile.arj", CompressionType.ArjLZ77)]
    [InlineData("Arj.method2.largefile.arj", CompressionType.ArjLZ77)]
    [InlineData("Arj.method3.largefile.arj", CompressionType.ArjLZ77)]
    [InlineData("Arj.method4.largefile.arj", CompressionType.ArjLZ77)]
    public async ValueTask Arj_LargeFileTest_Read_Async(
        string fileName,
        CompressionType compressionType
    )
    {
        await ReadForBufferBoundaryCheckAsync(fileName, compressionType);
    }

    [Theory]
    [InlineData("Arj.method1.arj")]
    [InlineData("Arj.method2.arj")]
    [InlineData("Arj.method3.arj")]
    [InlineData("Arj.method4.arj")]
    public async ValueTask Arj_CompressedEntryOver2GB_Throws_Async(string fileName)
    {
        using var stream = new MemoryStream(
            ArjTestArchives.WithFirstEntryOriginalSize(
                Path.Combine(TEST_ARCHIVES_PATH, fileName),
                0xC0000000
            )
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            ReaderOptions.ForExternalStream
        );
        Assert.True(await reader.MoveToNextEntryAsync());

        Assert.Equal(0xC0000000L, reader.Entry.Size);
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await reader.OpenEntryStreamAsync()
        );
    }

    [Theory]
    [InlineData("Arj.method1.largefile.arj", 1)]
    [InlineData("Arj.method1.largefile.arj", 4093)]
    [InlineData("Arj.method2.largefile.arj", 13)]
    [InlineData("Arj.method3.largefile.arj", 13)]
    public async ValueTask Arj_LargeFile_ReadInSmallChunks_Async(string fileName, int chunkSize)
    {
        using var cts = new CancellationTokenSource();
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, fileName));
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            ReaderOptions.ForExternalStream,
            cts.Token
        );
        Assert.True(await reader.MoveToNextEntryAsync(cts.Token));

        await using var entryStream = await reader.OpenEntryStreamAsync(cts.Token);
        using var output = new MemoryStream();

        // Read into the middle of a larger buffer to verify offset and count are honoured.
        var buffer = new byte[chunkSize + 10];
        int read;
        while ((read = await entryStream.ReadAsync(buffer, 5, chunkSize, cts.Token)) > 0)
        {
            output.Write(buffer, 5, read);
        }

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(MISC_TEST_FILES_PATH, "alice29.txt")),
            output.ToArray()
        );
    }

#if !LEGACY_DOTNET
    [Theory]
    [InlineData("Arj.method1.largefile.arj", 13)]
    [InlineData("Arj.method1.largefile.arj", 4093)]
    public async ValueTask Arj_LargeFile_ReadNonArrayMemoryInSmallChunks_Async(
        string fileName,
        int chunkSize
    )
    {
        using var cts = new CancellationTokenSource();
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, fileName));
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            ReaderOptions.ForExternalStream,
            cts.Token
        );
        Assert.True(await reader.MoveToNextEntryAsync(cts.Token));

        await using var entryStream = await reader.OpenEntryStreamAsync(cts.Token);
        using var output = new MemoryStream();

        var bytes = new byte[chunkSize];
        var memory = new NonArrayMemory(bytes).Memory;
        int read;
        while ((read = await entryStream.ReadAsync(memory, cts.Token)) > 0)
        {
            output.Write(bytes, 0, read);
        }

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(MISC_TEST_FILES_PATH, "alice29.txt")),
            output.ToArray()
        );
    }
#endif

    [Theory]
    [InlineData("Arj.method1.largefile.arj", false)]
#if !LEGACY_DOTNET
    [InlineData("Arj.method1.largefile.arj", true)]
#endif
    public async ValueTask Arj_LargeFile_CanceledReadDoesNotConsumeDecoderState_Async(
        string fileName,
        bool useMemory
    )
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, fileName));
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            ReaderOptions.ForExternalStream
        );
        Assert.True(await reader.MoveToNextEntryAsync());

        await using var entryStream = await reader.OpenEntryStreamAsync();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        var expected = File.ReadAllBytes(Path.Combine(MISC_TEST_FILES_PATH, "alice29.txt"));
        var buffer = new byte[1];

        // Interleaving a canceled read before every byte hits reads that are served from a
        // pending back-reference, which never reach the underlying stream. The first one
        // starts at byte 3 of this file.
        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await ReadOneByteAsync(entryStream, buffer, useMemory, canceled.Token)
            );

            Assert.Equal(1, await ReadOneByteAsync(entryStream, buffer, useMemory, default));
            Assert.Equal(expected[i], buffer[0]);
        }
    }

    [Fact]
    public async ValueTask Arj_LargeFile_ReadCanceledWhileDecoding_FaultsStream_Async()
    {
        using var fileStream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Arj.method1.largefile.arj")
        );
        var stream = new CancelOnReadStream(fileStream);
        await using var reader = await ReaderFactory.OpenAsyncReader(
            stream,
            ReaderOptions.ForExternalStream
        );
        Assert.True(await reader.MoveToNextEntryAsync());

        var entryStream = await reader.OpenEntryStreamAsync();
        var buffer = new byte[200_000];
        Assert.Equal(1000, await entryStream.ReadAsync(buffer, 0, 1000));

        using var cts = new CancellationTokenSource();
        stream.CancelOnNextRead = cts;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await entryStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await entryStream.ReadAsync(buffer, 0, buffer.Length)
        );

        await entryStream.DisposeAsync();
        Assert.True(reader.Cancelled);
        await Assert.ThrowsAsync<ReaderCancelledException>(async () =>
            await reader.MoveToNextEntryAsync()
        );
    }

    private static async Task<int> ReadOneByteAsync(
        Stream stream,
        byte[] buffer,
        bool useMemory,
        CancellationToken cancellationToken
    )
    {
#if !LEGACY_DOTNET
        if (useMemory)
        {
            return await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
        }
#endif
        return await stream.ReadAsync(buffer, 0, 1, cancellationToken);
    }

    private async Task ReadAsync(string testArchive, CompressionType? expectedCompression = null)
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using Stream stream = File.OpenRead(testArchive);
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            ReaderOptions.ForExternalStream
        );
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                if (expectedCompression.HasValue)
                {
                    Assert.Equal(expectedCompression.Value, reader.Entry.CompressionType);
                }
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        VerifyFiles();
    }

    private async Task ReadForBufferBoundaryCheckAsync(
        string testArchive,
        CompressionType expectedCompression
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using Stream stream = File.OpenRead(testArchive);
        await using var reader = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(stream),
            ReaderOptions.ForExternalStream with
            {
                LookForHeader = true,
            }
        );
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }
        CompareFilesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "alice29.txt"),
            Path.Combine(MISC_TEST_FILES_PATH, "alice29.txt")
        );
    }

    private async Task DoMultiReaderAsync(
        string[] archiveNames,
        Func<IEnumerable<Stream>, ValueTask<IAsyncReader>> openReader
    )
    {
        var testArchives = archiveNames.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s)).ToList();
        var streams = testArchives.Select(File.OpenRead).ToList();
        try
        {
            await using var reader = await openReader(streams);
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
                }
            }
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    private sealed class CancelOnReadStream(Stream stream) : AsyncOnlyStream(stream, false)
    {
        public CancellationTokenSource? CancelOnNextRead { get; set; }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            CancelIfRequested(cancellationToken);
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if NET8_0_OR_GREATER
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            CancelIfRequested(cancellationToken);
            return base.ReadAsync(buffer, cancellationToken);
        }
#endif

        private void CancelIfRequested(CancellationToken cancellationToken)
        {
            if (CancelOnNextRead is { } cts)
            {
                CancelOnNextRead = null;
                cts.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

#if !LEGACY_DOTNET
    private sealed class NonArrayMemory(byte[] bytes) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => bytes;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
#endif
}
