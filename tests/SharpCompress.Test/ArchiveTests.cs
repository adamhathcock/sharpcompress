using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
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
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                var memory = new MemoryStream();
                reader.WriteEntryTo(memory);

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
}
