using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using SharpCompress.Crypto;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class ArchiveTests : ReaderTests
{
    protected void ArchiveGetParts(IEnumerable<string> testArchives)
    {
        var arcs = testArchives.Select(a => Path.Combine(TEST_ARCHIVES_PATH, a)).ToArray();
        var found = ArchiveFactory.GetFileParts(arcs[0]).ToArray();
        Assert.Equal(arcs.Length, found.Length);
        for (var i = 0; i < arcs.Length; i++)
        {
            Assert.Equal(arcs[i], found[i]);
        }
    }

    protected void ArchiveStreamReadExtractAll(string testArchive, CompressionType compression)
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        ArchiveStreamReadExtractAll(new[] { testArchive }, compression);
    }

    protected void ArchiveStreamReadExtractAll(
        IEnumerable<string> testArchives,
        CompressionType compression
    )
    {
        foreach (var path in testArchives)
        {
            using (
                var stream = SharpCompressStream.Create(
                    File.OpenRead(path),
                    leaveOpen: true,
                    throwOnDispose: true
                )
            )
            {
                try
                {
                    using var archive = ArchiveFactory.Open(stream);
                    Assert.True(archive.IsSolid);
                    using (var reader = archive.ExtractAllEntries())
                    {
                        UseReader(reader, compression);
                    }
                    VerifyFiles();

                    if (archive.Entries.First().CompressionType == CompressionType.Rar)
                    {
                        stream.ThrowOnDispose = false;
                        return;
                    }
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                    stream.ThrowOnDispose = false;
                }
                catch (Exception)
                {
                    // Otherwise this will hide the original exception.
                    stream.ThrowOnDispose = false;
                    throw;
                }
            }
            VerifyFiles();
        }
    }

    protected void ArchiveStreamRead(string testArchive, ReaderOptions? readerOptions = null) =>
        ArchiveStreamRead(ArchiveFactory.AutoFactory, testArchive, readerOptions);

    protected void ArchiveStreamRead(
        IArchiveFactory archiveFactory,
        string testArchive,
        ReaderOptions? readerOptions = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        ArchiveStreamRead(archiveFactory, readerOptions, testArchive);
    }

    protected void ArchiveStreamRead(
        ReaderOptions? readerOptions = null,
        params string[] testArchives
    ) => ArchiveStreamRead(ArchiveFactory.AutoFactory, readerOptions, testArchives);

    protected void ArchiveStreamRead(
        IArchiveFactory archiveFactory,
        ReaderOptions? readerOptions = null,
        params string[] testArchives
    ) =>
        ArchiveStreamRead(
            archiveFactory,
            readerOptions,
            testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x))
        );

    protected void ArchiveStreamRead(
        IArchiveFactory archiveFactory,
        ReaderOptions? readerOptions,
        IEnumerable<string> testArchives
    )
    {
        foreach (var path in testArchives)
        {
            using (
                var stream = SharpCompressStream.Create(
                    File.OpenRead(path),
                    leaveOpen: true,
                    throwOnDispose: true
                )
            )
            using (var archive = archiveFactory.Open(stream, readerOptions))
            {
                try
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    //SevenZipArchive_BZip2_Split test needs this
                    stream.ThrowOnDispose = false;
                    throw;
                }
                stream.ThrowOnDispose = false;
            }
            VerifyFiles();
        }
    }

    protected void ArchiveStreamMultiRead(
        ReaderOptions? readerOptions = null,
        params string[] testArchives
    ) =>
        ArchiveStreamMultiRead(
            readerOptions,
            testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x))
        );

    protected void ArchiveStreamMultiRead(
        ReaderOptions? readerOptions,
        IEnumerable<string> testArchives
    )
    {
        using (
            var archive = ArchiveFactory.Open(
                testArchives.Select(a => new FileInfo(a)),
                readerOptions
            )
        )
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    protected void ArchiveOpenStreamRead(
        ReaderOptions? readerOptions = null,
        params string[] testArchives
    ) =>
        ArchiveOpenStreamRead(
            readerOptions,
            testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x))
        );

    protected void ArchiveOpenStreamRead(
        ReaderOptions? readerOptions,
        IEnumerable<string> testArchives
    )
    {
        using (
            var archive = ArchiveFactory.Open(
                testArchives.Select(f => new FileInfo(f)),
                readerOptions
            )
        )
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    protected void ArchiveOpenEntryVolumeIndexTest(
        int[][] results,
        ReaderOptions? readerOptions = null,
        params string[] testArchives
    ) =>
        ArchiveOpenEntryVolumeIndexTest(
            results,
            readerOptions,
            testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x))
        );

    private void ArchiveOpenEntryVolumeIndexTest(
        int[][] results,
        ReaderOptions? readerOptions,
        IEnumerable<string> testArchives
    )
    {
        var src = testArchives.ToArray();
        using var archive = ArchiveFactory.Open(src.Select(f => new FileInfo(f)), readerOptions);
        var idx = 0;
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            Assert.Equal(entry.VolumeIndexFirst, results[idx][0]);
            Assert.Equal(entry.VolumeIndexLast, results[idx][1]);
            Assert.Equal(
                src[entry.VolumeIndexFirst],
                archive.Volumes.First(a => a.Index == entry.VolumeIndexFirst).FileName
            );
            Assert.Equal(
                src[entry.VolumeIndexLast],
                archive.Volumes.First(a => a.Index == entry.VolumeIndexLast).FileName
            );

            idx++;
        }
    }

    protected void ArchiveExtractToDirectory(
        string testArchive,
        ReaderOptions? readerOptions = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using (var archive = ArchiveFactory.Open(new FileInfo(testArchive), readerOptions))
        {
            archive.WriteToDirectory(SCRATCH_FILES_PATH);
        }
        VerifyFiles();
    }

    protected void ArchiveFileRead(
        IArchiveFactory archiveFactory,
        string testArchive,
        ReaderOptions? readerOptions = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using (var archive = archiveFactory.Open(new FileInfo(testArchive), readerOptions))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    protected void ArchiveFileRead(string testArchive, ReaderOptions? readerOptions = null) =>
        ArchiveFileRead(ArchiveFactory.AutoFactory, testArchive, readerOptions);

    protected void ArchiveFileSkip(
        string testArchive,
        string fileOrder,
        ReaderOptions? readerOptions = null
    )
    {
        if (!Environment.OSVersion.IsWindows())
        {
            fileOrder = fileOrder.Replace('\\', '/');
        }
        var expected = new Stack<string>(fileOrder.Split(' '));
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using var archive = ArchiveFactory.Open(testArchive, readerOptions);
        foreach (var entry in archive.Entries)
        {
            Assert.Equal(expected.Pop(), entry.Key);
        }
    }

    /// <summary>
    /// Demonstrate the ExtractionOptions.PreserveFileTime and ExtractionOptions.PreserveAttributes extract options
    /// </summary>
    protected void ArchiveFileReadEx(string testArchive)
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using (var archive = ArchiveFactory.Open(testArchive))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true,
                        PreserveAttributes = true,
                        PreserveFileTime = true,
                    }
                );
            }
        }
        VerifyFilesEx();
    }

    protected void ArchiveDeltaDistanceRead(string testArchive)
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using var archive = ArchiveFactory.Open(testArchive);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                var memory = new MemoryStream();
                entry.WriteTo(memory);

                memory.Position = 0;

                for (var y = 0; y < 9; y++)
                for (var x = 0; x < 256; x++)
                {
                    Assert.Equal(x, memory.ReadByte());
                }

                Assert.Equal(-1, memory.ReadByte());
            }
        }
    }

    /// <summary>
    /// Calculates CRC32 for the given data using SharpCompress implementation
    /// </summary>
    protected static uint CalculateCrc32(byte[] data) => Crc32.Compute(data);

    /// <summary>
    /// Creates a writer with the specified compression type and level
    /// </summary>
    protected static IWriter CreateWriterWithLevel(
        Stream stream,
        CompressionType compressionType,
        int? compressionLevel = null
    )
    {
        var writerOptions = new ZipWriterOptions(compressionType);
        if (compressionLevel.HasValue)
        {
            writerOptions.CompressionLevel = compressionLevel.Value;
        }
        return WriterFactory.Open(stream, ArchiveType.Zip, writerOptions);
    }

    /// <summary>
    /// Verifies archive content against expected files with CRC32 validation
    /// </summary>
    protected void VerifyArchiveContent(
        MemoryStream zipStream,
        Dictionary<string, (byte[] data, uint crc)> expectedFiles
    )
    {
        zipStream.Position = 0;
        using var archive = ArchiveFactory.Open(zipStream);
        Assert.Equal(expectedFiles.Count, archive.Entries.Count());

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            using var extractedStream = new MemoryStream();
            entryStream.CopyTo(extractedStream);
            var extractedData = extractedStream.ToArray();

            Assert.True(
                expectedFiles.ContainsKey(entry.Key.NotNull()),
                $"Unexpected entry: {entry.Key}"
            );

            var (expectedData, expectedCrc) = expectedFiles[entry.Key.NotNull()];
            var actualCrc = CalculateCrc32(extractedData);

            Assert.Equal(expectedCrc, actualCrc);
            Assert.Equal(expectedData.Length, extractedData.Length);

            // For large files, spot check rather than full comparison for performance
            if (expectedData.Length > 1024 * 1024)
            {
                VerifyDataSpotCheck(expectedData, extractedData);
            }
            else
            {
                Assert.Equal(expectedData, extractedData);
            }
        }
    }

    /// <summary>
    /// Performs efficient spot checks on large data arrays
    /// </summary>
    protected static void VerifyDataSpotCheck(byte[] expected, byte[] actual)
    {
        // Check first, middle, and last 1KB
        Assert.Equal(expected.Take(1024), actual.Take(1024));
        var mid = expected.Length / 2;
        Assert.Equal(expected.Skip(mid).Take(1024), actual.Skip(mid).Take(1024));
        Assert.Equal(
            expected.Skip(Math.Max(0, expected.Length - 1024)),
            actual.Skip(Math.Max(0, actual.Length - 1024))
        );
    }

    /// <summary>
    /// Verifies compression ratio meets expectations
    /// </summary>
    protected void VerifyCompressionRatio(
        long originalSize,
        long compressedSize,
        double maxRatio,
        string context
    )
    {
        var compressionRatio = (double)compressedSize / originalSize;
        Assert.True(
            compressionRatio < maxRatio,
            $"Expected better compression for {context}. Original: {originalSize}, Compressed: {compressedSize}, Ratio: {compressionRatio:P}"
        );
    }

    /// <summary>
    /// Creates a memory-based archive with specified files and compression
    /// </summary>
    protected MemoryStream CreateMemoryArchive(
        Dictionary<string, byte[]> files,
        CompressionType compressionType,
        int? compressionLevel = null
    )
    {
        var zipStream = new MemoryStream();
        using (var writer = CreateWriterWithLevel(zipStream, compressionType, compressionLevel))
        {
            foreach (var kvp in files)
            {
                writer.Write(kvp.Key, new MemoryStream(kvp.Value));
            }
        }
        return zipStream;
    }

    /// <summary>
    /// Verifies streaming CRC calculation for large data
    /// </summary>
    protected void VerifyStreamingCrc(Stream entryStream, uint expectedCrc, long expectedLength)
    {
        using var crcStream = new Crc32Stream(Stream.Null);
        const int bufferSize = 64 * 1024;
        var buffer = new byte[bufferSize];
        int totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = entryStream.Read(buffer, 0, bufferSize)) > 0)
        {
            crcStream.Write(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
        }

        var actualCrc = crcStream.Crc;
        Assert.Equal(expectedCrc, actualCrc);
        Assert.Equal(expectedLength, totalBytesRead);
    }

    /// <summary>
    /// Creates and verifies a basic archive with compression testing
    /// </summary>
    protected void CreateAndVerifyBasicArchive(
        Dictionary<string, byte[]> testFiles,
        CompressionType compressionType,
        int? compressionLevel = null,
        double maxCompressionRatio = 0.8
    )
    {
        // Calculate expected CRCs
        var expectedFiles = testFiles.ToDictionary(
            kvp => kvp.Key,
            kvp => (data: kvp.Value, crc: CalculateCrc32(kvp.Value))
        );

        // Create archive
        using var zipStream = CreateMemoryArchive(testFiles, compressionType, compressionLevel);

        // Verify compression occurred if expected
        if (compressionType != CompressionType.None)
        {
            var originalSize = testFiles.Values.Sum(data => (long)data.Length);
            VerifyCompressionRatio(
                originalSize,
                zipStream.Length,
                maxCompressionRatio,
                compressionType.ToString()
            );
        }

        // Verify content
        VerifyArchiveContent(zipStream, expectedFiles);
    }

    /// <summary>
    /// Verifies archive entries have correct compression type
    /// </summary>
    protected void VerifyCompressionType(
        MemoryStream zipStream,
        CompressionType expectedCompressionType
    )
    {
        zipStream.Position = 0;
        using var archive = ArchiveFactory.Open(zipStream);

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            Assert.Equal(expectedCompressionType, entry.CompressionType);
        }
    }

    /// <summary>
    /// Extracts and verifies a single entry from archive
    /// </summary>
    protected (byte[] data, uint crc) ExtractAndVerifyEntry(
        MemoryStream zipStream,
        string entryName
    )
    {
        zipStream.Position = 0;
        using var archive = ArchiveFactory.Open(zipStream);

        var entry = archive.Entries.FirstOrDefault(e => e.Key == entryName && !e.IsDirectory);
        Assert.NotNull(entry);

        using var entryStream = entry.OpenEntryStream();
        using var extractedStream = new MemoryStream();
        entryStream.CopyTo(extractedStream);

        var extractedData = extractedStream.ToArray();
        var crc = CalculateCrc32(extractedData);

        return (extractedData, crc);
    }

    protected async Task ArchiveStreamReadAsync(
        string testArchive,
        ReaderOptions? readerOptions = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        await ArchiveStreamReadAsync(
            ArchiveFactory.AutoFactory,
            readerOptions,
            new[] { testArchive }
        );
    }

    protected async Task ArchiveStreamReadAsync(
        IArchiveFactory archiveFactory,
        ReaderOptions? readerOptions,
        IEnumerable<string> testArchives
    )
    {
        foreach (var path in testArchives)
        {
            using (
                var stream = SharpCompressStream.Create(
                    File.OpenRead(path),
                    leaveOpen: true,
                    throwOnDispose: true
                )
            )
            using (var archive = archiveFactory.Open(stream, readerOptions))
            {
                try
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        await entry.WriteToDirectoryAsync(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    //SevenZipArchive_BZip2_Split test needs this
                    stream.ThrowOnDispose = false;
                    throw;
                }
                stream.ThrowOnDispose = false;
            }
            VerifyFiles();
        }
    }

    [Fact]
    public void ArchiveFactory_Open_WithPreWrappedStream()
    {
        // Test that ArchiveFactory.Open works correctly with a stream that's already wrapped
        // This addresses the issue where ZIP files fail to open on Linux
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.noEmptyDirs.zip");

        // Open with a pre-wrapped stream
        using (var fileStream = File.OpenRead(testArchive))
        using (var wrappedStream = SharpCompressStream.Create(fileStream, bufferSize: 32768))
        using (var archive = ArchiveFactory.Open(wrappedStream))
        {
            Assert.Equal(ArchiveType.Zip, archive.Type);
            Assert.Equal(3, archive.Entries.Count());
        }
    }

    [Fact]
    public void ArchiveFactory_Open_WithRawFileStream()
    {
        // Test that ArchiveFactory.Open works correctly with a raw FileStream
        // This is the common use case reported in the issue
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.noEmptyDirs.zip");

        using (var stream = File.OpenRead(testArchive))
        using (var archive = ArchiveFactory.Open(stream))
        {
            Assert.Equal(ArchiveType.Zip, archive.Type);
            Assert.Equal(3, archive.Entries.Count());
        }
    }
}
