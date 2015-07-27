using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archive;
using SharpCompress.Common;

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
                using (Stream stream = File.OpenRead(path))
                using (var archive = ArchiveFactory.Open(stream))
                {
                    Assert.IsTrue(archive.IsSolid);
                    using (var reader = archive.ExtractAllEntries())
                    {
                        ReaderTests.UseReader(this, reader, compression);
                    }
                    VerifyFiles();

                    if (archive.Entries.First().CompressionType == CompressionType.Rar)
                    {
                        return;
                    }
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
                VerifyFiles();
            }
        }

        protected void ArchiveStreamRead(string testArchive)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveStreamRead(testArchive.AsEnumerable());
        }

        protected void ArchiveStreamRead(params string[] testArchives)
        {
            ArchiveStreamRead(testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x)));
        }

        protected void ArchiveStreamRead(IEnumerable<string> testArchives)
        {
            foreach (var path in testArchives)
            {
                ResetScratch();
                using (Stream stream = File.OpenRead(path))
                using (var archive = ArchiveFactory.Open(stream))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
                VerifyFiles();
            }
        }

        protected void ArchiveFileRead(string testArchive)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            ArchiveFileRead(testArchive.AsEnumerable());
        }
        protected void ArchiveFileRead(IEnumerable<string> testArchives)
        {
            foreach (var path in testArchives)
            {
                ResetScratch();
                using (var archive = ArchiveFactory.Open(path))
                {
                    archive.EntryExtractionBegin += archive_EntryExtractionBegin;
                    archive.FilePartExtractionBegin += archive_FilePartExtractionBegin;
                    archive.CompressedBytesRead += archive_CompressedBytesRead;

                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
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
            this.partTotal = e.Size;
            Console.WriteLine("Initializing File Part Extraction: " + e.Name);
        }

        void archive_EntryExtractionBegin(object sender, ArchiveExtractionEventArgs<IArchiveEntry> e)
        {
            this.entryTotal = e.Item.Size;
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
        /// Demonstrate the TotalUncompressSize property, and the ExtractOptions.PreserveFileTime and ExtractOptions.PreserveAttributes extract options
        /// </summary>
        protected void ArchiveFileReadEx(IEnumerable<string> testArchives)
        {
            foreach (var path in testArchives)
            {
                ResetScratch();
                using (var archive = ArchiveFactory.Open(path))
                {
                    this.totalSize = archive.TotalUncompressSize;
                    archive.EntryExtractionBegin += Archive_EntryExtractionBeginEx;
                    archive.EntryExtractionEnd += Archive_EntryExtractionEndEx;
                    archive.CompressedBytesRead += Archive_CompressedBytesReadEx;

                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite | ExtractOptions.PreserveFileTime | ExtractOptions.PreserveAttributes);
                    }
                }
                VerifyFilesEx();
            }
        }

        private void Archive_EntryExtractionEndEx(object sender, ArchiveExtractionEventArgs<IArchiveEntry> e)
        {
            this.partTotal += e.Item.Size;
        }

        private void Archive_CompressedBytesReadEx(object sender, CompressedBytesReadEventArgs e)
        {
            string percentage = this.entryTotal.HasValue ? this.CreatePercentage(e.CompressedBytesRead, this.entryTotal.Value).ToString() : "-";
            string tortalPercentage = this.CreatePercentage(this.partTotal + e.CompressedBytesRead, this.totalSize).ToString();
            Console.WriteLine(@"Read Compressed File Progress: {0}% Total Progress {1}%", percentage, tortalPercentage);
        }

        private void Archive_EntryExtractionBeginEx(object sender, ArchiveExtractionEventArgs<IArchiveEntry> e)
        {
            this.entryTotal = e.Item.Size;
        }

        private int CreatePercentage(long n, long d)
        {
            return (int)(((double)n / (double)d) * 100);
        }
    }
}
