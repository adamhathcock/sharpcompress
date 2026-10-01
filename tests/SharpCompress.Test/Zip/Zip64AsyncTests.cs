using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class Zip64AsyncTests : WriterTests
{
    public Zip64AsyncTests()
        : base(ArchiveType.Zip) { }

    // 4GiB + 1
    private const long FOUR_GB_LIMIT = ((long)uint.MaxValue) + 1;

    //[Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Single_Large_File_Async() =>
        await RunSingleTestAsync(1, FOUR_GB_LIMIT, setZip64: true, forwardOnly: false);

    //[Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Two_Large_Files_Async() =>
        await RunSingleTestAsync(2, FOUR_GB_LIMIT, setZip64: true, forwardOnly: false);

    //[Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Two_Small_files_Async() =>
        // Multiple files, does not require zip64
        await RunSingleTestAsync(2, FOUR_GB_LIMIT / 2, setZip64: false, forwardOnly: false);

    // [Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Two_Small_files_stream_Async() =>
        await RunSingleTestAsync(2, FOUR_GB_LIMIT / 2, setZip64: false, forwardOnly: true);

    // [Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Two_Small_Files_Zip64_Async() =>
        // Multiple files, use zip64 even though it is not required
        await RunSingleTestAsync(2, FOUR_GB_LIMIT / 2, setZip64: true, forwardOnly: false);

    //  [Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Single_Large_File_Fail_Async()
    {
        try
        {
            // One single file, should fail
            await RunSingleTestAsync(1, FOUR_GB_LIMIT, setZip64: false, forwardOnly: false);
            throw new InvalidOperationException("Test did not fail?");
        }
        catch (NotSupportedException) { }
    }

    // [Fact]
    [Trait("zip64", "true")]
    public async ValueTask Zip64_Single_Large_File_Zip64_Streaming_Fail_Async()
    {
        try
        {
            // One single file, should fail (fast) with zip64
            await RunSingleTestAsync(1, FOUR_GB_LIMIT, setZip64: true, forwardOnly: true);
            throw new InvalidOperationException("Test did not fail?");
        }
        catch (NotSupportedException) { }
    }

    // [Fact]
    [Trait("zip64", "true")]
    public async ValueTask Zip64_Single_Large_File_Streaming_Fail_Async()
    {
        try
        {
            // One single file, should fail once the write discovers the problem
            await RunSingleTestAsync(1, FOUR_GB_LIMIT, setZip64: false, forwardOnly: true);
            throw new InvalidOperationException("Test did not fail?");
        }
        catch (NotSupportedException) { }
    }

    // Regression test for reading a Zip64 archive over a *non-seekable* async stream, as
    // happens when extracting directly from a network download. When a >=4GB (Zip64) entry
    // is followed by another entry, the streaming reader probes a few bytes past the big
    // entry's data to locate the next header and must rewind them. For non-seekable streams
    // it previously failed to do so (the rewind was gated on SeekableSharpCompressStream),
    // leaving the reader misaligned so the *following* local header was parsed from garbage
    // and extraction threw near the very end. A seekable stream rewinds correctly and works,
    // which is exactly the "works seekable, fails non-seekable" symptom that was reported.
    //
    // NOTE: heavy (~4GB) like the other Zip64 large-file tests in this file, hence disabled
    // by default. Enable to verify the fix.
    //[Fact]
    [Trait("format", "zip64")]
    public async ValueTask Zip64_Large_File_Then_Small_File_NonSeekable_Async()
    {
        var filename = Path.Combine(SCRATCH2_FILES_PATH, "zip64-nonseekable-async.zip");

        // A small trailing entry with recognizable content. Its bytes can only be read back
        // correctly if the reader stays byte-aligned after the preceding >=4GB Zip64 entry.
        var smallContent = new byte[64 * 1024];
        for (var i = 0; i < smallContent.Length; i++)
        {
            smallContent[i] = (byte)(i % 251);
        }

        try
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            CreateLargeThenSmallZip(filename, FOUR_GB_LIMIT, smallContent);

            var (count, lastKey, lastContent) = await ReadLargeThenSmallNonSeekableAsync(filename);

            // The reader must reach the second (small) entry without throwing, identify it
            // correctly, and read its bytes verbatim.
            Assert.Equal(2, count);
            Assert.Equal("small", lastKey);
            Assert.NotNull(lastContent);
            Assert.Equal(smallContent, lastContent!);
        }
        finally
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
        }
    }

    private void CreateLargeThenSmallZip(string filename, long largeSize, byte[] smallContent)
    {
        var chunk = new byte[1024 * 1024];

        // Force Zip64 and store (level 0) so the large entry's compressed size also exceeds
        // 4GiB, which is what marks the entry as Zip64 for the streaming reader.
        var opts = new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = true };
        var eo = new ZipWriterEntryOptions { CompressionLevel = 0 };

        using var zip = File.OpenWrite(filename);
        using var zipWriter = (ZipWriter)WriterFactory.OpenWriter(zip, ArchiveType.Zip, opts);

        using (var str = zipWriter.WriteToStream("large", eo))
        {
            var left = largeSize;
            while (left > 0)
            {
                var b = (int)Math.Min(left, chunk.Length);
                str.Write(chunk, 0, b);
                left -= b;
            }
        }

        using (var str = zipWriter.WriteToStream("small", eo))
        {
            str.Write(smallContent, 0, smallContent.Length);
        }
    }

    private async ValueTask<(
        long Count,
        string? LastKey,
        byte[]? LastContent
    )> ReadLargeThenSmallNonSeekableAsync(string filename)
    {
        long count = 0;
        string? lastKey = null;
        byte[]? lastContent = null;

        using var fs = File.OpenRead(filename);
        // ForwardOnlyStream reports CanSeek == false; AsyncOnlyStream forces async reads.
        // Together they emulate a non-seekable, async-only source (e.g. a network download).
        //
        // IMPORTANT: use default ReaderOptions (LeaveStreamOpen == false), exactly as the
        // reporting user did. With LeaveStreamOpen == true the Volume wraps the stream in a
        // passthrough that Create() later unwraps into a SeekableSharpCompressStream, which
        // happens to take the working seek-back path and hides the bug. The default keeps a
        // plain ring-buffer SharpCompressStream, which is where the streaming reader fails.
        await using var rd = await ReaderFactory.OpenAsyncReader(
            new AsyncOnlyStream(new ForwardOnlyStream(fs)),
            new ReaderOptions { LookForHeader = false }
        );
        while (await rd.MoveToNextEntryAsync())
        {
            count++;
            lastKey = rd.Entry.Key;

            await using var entryStream = await rd.OpenEntryStreamAsync();
            if (rd.Entry.Key == "small")
            {
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
                lastContent = ms.ToArray();
            }
            else
            {
                await entryStream.SkipEntryAsync();
            }
        }

        return (count, lastKey, lastContent);
    }

    public async ValueTask RunSingleTestAsync(
        long files,
        long filesize,
        bool setZip64,
        bool forwardOnly,
        long writeChunkSize = 1024 * 1024,
        string filename = "zip64-test-async.zip"
    )
    {
        filename = Path.Combine(SCRATCH2_FILES_PATH, filename);

        try
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            if (!File.Exists(filename))
            {
                await CreateZipArchiveAsync(
                    filename,
                    files,
                    filesize,
                    writeChunkSize,
                    setZip64,
                    forwardOnly
                );
            }

            var resForward = await ReadForwardOnlyAsync(filename);
            if (resForward.Item1 != files)
            {
                throw new InvalidOperationException(
                    $"Incorrect number of items reported: {resForward.Item1}, should have been {files}"
                );
            }

            if (resForward.Item2 != files * filesize)
            {
                throw new InvalidOperationException(
                    $"Incorrect combined size reported: {resForward.Item2}, should have been {files * filesize}"
                );
            }

            var resArchive = ReadArchive(filename);
            if (resArchive.Item1 != files)
            {
                throw new InvalidOperationException(
                    $"Incorrect number of items reported: {resArchive.Item1}, should have been {files}"
                );
            }

            if (resArchive.Item2 != files * filesize)
            {
                throw new InvalidOperationException(
                    $"Incorrect number of items reported: {resArchive.Item2}, should have been {files * filesize}"
                );
            }
        }
        finally
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
        }
    }

    public async ValueTask CreateZipArchiveAsync(
        string filename,
        long files,
        long filesize,
        long chunksize,
        bool setZip64,
        bool forwardOnly
    )
    {
        var data = new byte[chunksize];

        // Use deflate for speed
        var opts = new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = setZip64 };

        // Use no compression to ensure we hit the limits (actually inflates a bit, but seems better than using method==Store)
        var eo = new ZipWriterEntryOptions { CompressionLevel = 0 };

        using var zip = File.OpenWrite(filename);
        using var st = forwardOnly ? (Stream)new ForwardOnlyStream(zip) : zip;
        using var zipWriter = (ZipWriter)WriterFactory.OpenWriter(st, ArchiveType.Zip, opts);
        for (var i = 0; i < files; i++)
        {
            using var str = zipWriter.WriteToStream(i.ToString(), eo);
            var left = filesize;
            while (left > 0)
            {
                var b = (int)Math.Min(left, data.Length);
                // Use synchronous Write to match the sync version and avoid ForwardOnlyStream issues
                await str.WriteAsync(data, 0, b);
                left -= b;
            }
        }
    }

    public async ValueTask<Tuple<long, long>> ReadForwardOnlyAsync(string filename)
    {
        long count = 0;
        long size = 0;
        ZipEntry? prev = null;
        using (var fs = File.OpenRead(filename))
        {
            await using var rd = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(fs),
                ReaderOptions.ForExternalStream with
                {
                    LookForHeader = false,
                }
            );
            while (await rd.MoveToNextEntryAsync())
            {
                await using (var entryStream = await rd.OpenEntryStreamAsync())
                {
                    await entryStream.SkipEntryAsync();
                }
                count++;
                if (prev != null)
                {
                    size += prev.Size;
                }

                prev = (ZipEntry)rd.Entry;
            }
        }

        if (prev != null)
        {
            size += prev.Size;
        }

        return new Tuple<long, long>(count, size);
    }

    public Tuple<long, long> ReadArchive(string filename)
    {
        using var archive = ArchiveFactory.OpenArchive(filename);
        return new Tuple<long, long>(
            archive.Entries.Count(),
            archive.Entries.Select(x => x.Size).Sum()
        );
    }
}
