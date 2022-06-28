using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test
{
    public class ArchiveTests : ReaderTests
    {
        protected void ArchiveGetParts(IEnumerable<string> testArchives)
        {
            string[] arcs = testArchives.Select(a => Path.Combine(TEST_ARCHIVES_PATH, a)).ToArray();
            string[] found = ArchiveFactory.GetFileParts(arcs[0]).ToArray();
            Assert.Equal(arcs.Length, found.Length);
            for (int i = 0; i < arcs.Length; i++)
                Assert.Equal(arcs[i], found[i]);
        }

        protected void ArchiveStreamReadExtractAll(string testArchive, CompressionType compression)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveStreamReadExtractAll(testArchive.AsEnumerable(), compression);
        }


        protected void ArchiveStreamReadExtractAll(IEnumerable<string> testArchives, CompressionType compression)
        {
            foreach (var path in testArchives)
            {
                using (var stream = NonDisposingStream.Create(File.OpenRead(path), true))
                {
                    try
                    {
                        using (var archive = ArchiveFactory.Open(stream))
                        {
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
                                entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                                       new ExtractionOptions
                                                       {
                                                           ExtractFullPath = true,
                                                           Overwrite = true
                                                       });
                            }
                            stream.ThrowOnDispose = false;
                        }
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

        protected void ArchiveStreamRead(string testArchive, ReaderOptions readerOptions = null)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveStreamRead(readerOptions, testArchive.AsEnumerable());
        }

        protected void ArchiveStreamRead(ReaderOptions readerOptions = null, params string[] testArchives)
        {
            ArchiveStreamRead(readerOptions, testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x)));
        }

        protected void ArchiveStreamRead(ReaderOptions readerOptions, IEnumerable<string> testArchives)
        {
            foreach (var path in testArchives)
            {
                using (var stream = NonDisposingStream.Create(File.OpenRead(path), true))
                using (var archive = ArchiveFactory.Open(stream, readerOptions))
                {
                    try
                    {
                        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                        {
                            entry.WriteToDirectory(SCRATCH_FILES_PATH,
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

        protected void ArchiveStreamMultiRead(ReaderOptions readerOptions = null, params string[] testArchives)
        {
            ArchiveStreamMultiRead(readerOptions, testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x)));
        }

        protected void ArchiveStreamMultiRead(ReaderOptions readerOptions, IEnumerable<string> testArchives)
        {
                using (var archive = ArchiveFactory.Open(testArchives.Select(a => new FileInfo(a)), readerOptions))
                {
                    try
                    {
                        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                        {
                            entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                                    new ExtractionOptions()
                                                    {
                                                        ExtractFullPath = true,
                                                        Overwrite = true
                                                    });
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw;
                    }
                }
                VerifyFiles();
        }

        protected void ArchiveOpenStreamRead(ReaderOptions readerOptions = null, params string[] testArchives)
        {
            ArchiveOpenStreamRead(readerOptions, testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x)));
        }

        protected void ArchiveOpenStreamRead(ReaderOptions readerOptions, IEnumerable<string> testArchives)
        {
            using (var archive = ArchiveFactory.Open(testArchives.Select(f => new FileInfo(f)), null))
            {
                try
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               new ExtractionOptions()
                                               {
                                                   ExtractFullPath = true,
                                                   Overwrite = true
                                               });
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    throw;
                }
            }
            VerifyFiles();
        }


        protected void ArchiveFileRead(string testArchive, ReaderOptions readerOptions = null)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            using (var archive = ArchiveFactory.Open(testArchive, readerOptions))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH,
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
        protected void ArchiveFileReadEx(string testArchive)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            using (var archive = ArchiveFactory.Open(testArchive))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH,
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
