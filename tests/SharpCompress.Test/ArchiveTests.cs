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
    public class ArchiveTests : TestBase
    {
        protected void ArchiveStreamReadExtractAll(string testArchive, CompressionType compression)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveStreamReadExtractAll(testArchive.AsEnumerable(), compression);
        }


        protected void ArchiveStreamReadExtractAll(IEnumerable<string> testArchives, CompressionType compression)
        {
            foreach (var path in testArchives)
            {
                ResetScratch();
                using (var stream = new NonDisposingStream(File.OpenRead(path), true))
                using (var archive = ArchiveFactory.Open(stream))
                {
                    Assert.True(archive.IsSolid);
                    using (var reader = archive.ExtractAllEntries())
                    {
                        ReaderTests.UseReader(this, reader, compression);
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
                ResetScratch();
                using (var stream = new NonDisposingStream(File.OpenRead(path), true))
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
                    catch (InvalidFormatException)
                    {
                        //rar SOLID test needs this
                        stream.ThrowOnDispose = false;
                        throw;
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

        protected void ArchiveFileRead(string testArchive, ReaderOptions readerOptions = null)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveFileRead(testArchive.AsEnumerable(), readerOptions);
        }

        protected void ArchiveFileRead(IEnumerable<string> testArchives, ReaderOptions readerOptions = null)
        {
            foreach (var path in testArchives)
            {
                ResetScratch();
                using (var archive = ArchiveFactory.Open(path, readerOptions))
                {
                    //archive.EntryExtractionBegin += archive_EntryExtractionBegin;
                    //archive.FilePartExtractionBegin += archive_FilePartExtractionBegin;
                    //archive.CompressedBytesRead += archive_CompressedBytesRead;

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
        }

        void archive_CompressedBytesRead(object sender, CompressedBytesReadEventArgs e)
        {
            Console.WriteLine("Read Compressed File Part Bytes: {0} Percentage: {1}%",
                e.CurrentFilePartCompressedBytesRead, CreatePercentage(e.CurrentFilePartCompressedBytesRead, partTotal));

            string percentage = entryTotal.HasValue ? CreatePercentage(e.CompressedBytesRead,
                entryTotal.Value).ToString() : "Unknown";
            Console.WriteLine("Read Compressed File Entry Bytes: {0} Percentage: {1}%",
                e.CompressedBytesRead, percentage);
        }

        void archive_FilePartExtractionBegin(object sender, FilePartExtractionBeginEventArgs e)
        {
            partTotal = e.Size;
            Console.WriteLine("Initializing File Part Extraction: " + e.Name);
        }

        void archive_EntryExtractionBegin(object sender, ArchiveExtractionEventArgs<IArchiveEntry> e)
        {
            entryTotal = e.Item.Size;
            Console.WriteLine("Initializing File Entry Extraction: " + e.Item.Key);
        }

        private long? entryTotal;
        private long partTotal;
        private long totalSize;

        protected void ArchiveFileReadEx(string testArchive)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveFileReadEx(testArchive.AsEnumerable());
        }
        
        /// <summary>
        /// Demonstrate the TotalUncompressSize property, and the ExtractionOptions.PreserveFileTime and ExtractionOptions.PreserveAttributes extract options
        /// </summary>
        protected void ArchiveFileReadEx(IEnumerable<string> testArchives)
        {
            foreach (var path in testArchives)
            {
                ResetScratch();
                using (var archive = ArchiveFactory.Open(path))
                {
                    totalSize = archive.TotalUncompressSize;
                    //archive.EntryExtractionBegin += Archive_EntryExtractionBeginEx;
                    //archive.EntryExtractionEnd += Archive_EntryExtractionEndEx;
                    //archive.CompressedBytesRead += Archive_CompressedBytesReadEx;

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

        private void Archive_EntryExtractionEndEx(object sender, ArchiveExtractionEventArgs<IArchiveEntry> e)
        {
            partTotal += e.Item.Size;
        }

        private void Archive_CompressedBytesReadEx(object sender, CompressedBytesReadEventArgs e)
        {
            string percentage = entryTotal.HasValue ? CreatePercentage(e.CompressedBytesRead, entryTotal.Value).ToString() : "-";
            string tortalPercentage = CreatePercentage(partTotal + e.CompressedBytesRead, totalSize).ToString();
            Console.WriteLine(@"Read Compressed File Progress: {0}% Total Progress {1}%", percentage, tortalPercentage);
        }

        private void Archive_EntryExtractionBeginEx(object sender, ArchiveExtractionEventArgs<IArchiveEntry> e)
        {
            entryTotal = e.Item.Size;
        }

        private int CreatePercentage(long n, long d)
        {
            return (int)(((double)n / (double)d) * 100);
        }
    }
}
