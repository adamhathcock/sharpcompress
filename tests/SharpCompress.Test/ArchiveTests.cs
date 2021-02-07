using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test
{
    public class ArchiveTests : ReaderTests
    {
        protected async ValueTask ArchiveStreamReadExtractAll(string testArchive, CompressionType compression)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            await ArchiveStreamReadExtractAll(testArchive.AsEnumerable(), compression);
        }


        protected async ValueTask ArchiveStreamReadExtractAll(IEnumerable<string> testArchives, CompressionType compression)
        {
            foreach (var path in testArchives)
            {
                await using (var stream = new NonDisposingStream(File.OpenRead(path), true))
                await using (var archive = await ArchiveFactory.OpenAsync(stream))
                {
                    Assert.True(await archive.IsSolidAsync());
                    await using (var reader = await archive.ExtractAllEntries())
                    {
                        await ReadAsync(reader, compression);
                    }
                    VerifyFiles();

                    if ((await archive.Entries.FirstAsync()).CompressionType == CompressionType.Rar)
                    {
                        stream.ThrowOnDispose = false;
                        return;
                    }
                    await foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        await entry.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                               new ExtractionOptions
                                               {
                                                   ExtractFullPath = true,
                                                   Overwrite = true
                                               });
                    }
                    stream.ThrowOnDispose = false;
                }
                VerifyFiles();
            }
        }

        protected ValueTask ArchiveStreamReadAsync(string testArchive, ReaderOptions readerOptions = null)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            return ArchiveStreamReadAsync(readerOptions, testArchive.AsEnumerable());
        }

        protected ValueTask ArchiveStreamReadAsync(ReaderOptions readerOptions = null, params string[] testArchives)
        {
            return ArchiveStreamReadAsync(readerOptions, testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x)));
        }

        protected async ValueTask ArchiveStreamReadAsync(ReaderOptions readerOptions, IEnumerable<string> testArchives)
        {
            foreach (var path in testArchives)
            {
                using (var stream = new NonDisposingStream(File.OpenRead(path), true))
                await using (var archive = await ArchiveFactory.OpenAsync(stream, readerOptions))
                {
                    try
                    {
                        await foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                        {
                            await entry.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                   new ExtractionOptions()
                                                   {
                                                       ExtractFullPath = true,
                                                       Overwrite = true
                                                   });
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

        protected async ValueTask ArchiveFileReadAsync(string testArchive, ReaderOptions readerOptions = null)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            await using (var archive = await ArchiveFactory.OpenAsync(testArchive, readerOptions))
            {
                await foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    await entry.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                        new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                }
            }
            VerifyFiles();
        }

        /// <summary>
        /// Demonstrate the ExtractionOptions.PreserveFileTime and ExtractionOptions.PreserveAttributes extract options
        /// </summary>
        protected async ValueTask ArchiveFileReadEx(string testArchive)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            await using (var archive = await ArchiveFactory.OpenAsync(testArchive))
            {
                await foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    await entry.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                        new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true,
                            PreserveAttributes = true,
                            PreserveFileTime = true
                        });
                }
            }
            VerifyFilesEx();
        }
    }
}
