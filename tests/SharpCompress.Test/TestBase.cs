using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test
{
    public class TestBase : IDisposable
    {
        private string SOLUTION_BASE_PATH;
        protected string TEST_ARCHIVES_PATH;
        protected string ORIGINAL_FILES_PATH;
        protected string MISC_TEST_FILES_PATH;
        private string SCRATCH_BASE_PATH;
        public string SCRATCH_FILES_PATH;
        protected string SCRATCH2_FILES_PATH;

        public TestBase()
        {
            var index = AppDomain.CurrentDomain.BaseDirectory.IndexOf("SharpCompress.Test", StringComparison.OrdinalIgnoreCase);
            SOLUTION_BASE_PATH = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.Substring(0, index));

            TEST_ARCHIVES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Archives");
            ORIGINAL_FILES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Original");
            MISC_TEST_FILES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "MiscTest");

            SCRATCH_BASE_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", Guid.NewGuid().ToString());
            SCRATCH_FILES_PATH = Path.Combine(SCRATCH_BASE_PATH, "Scratch");
            SCRATCH2_FILES_PATH = Path.Combine(SCRATCH_BASE_PATH, "Scratch2");

            Directory.CreateDirectory(SCRATCH_FILES_PATH);
            Directory.CreateDirectory(SCRATCH2_FILES_PATH);
        }

        public void Dispose()
        {
            Directory.Delete(SCRATCH_BASE_PATH, true);
        }

        public void VerifyFiles()
        {
            if (UseExtensionInsteadOfNameToVerify)
            {
                VerifyFilesByExtension();
            }
            else
            {
                VerifyFilesByName();
            }
        }

        /// <summary>
        /// Verifies the files also check modified time and attributes.
        /// </summary>
        public void VerifyFilesEx()
        {
            if (UseExtensionInsteadOfNameToVerify)
            {
                VerifyFilesByExtensionEx();
            }
            else
            {
                VerifyFilesByNameEx();
            }
        }

        protected void VerifyFilesByName()
        {
            var extracted =
                Directory.EnumerateFiles(SCRATCH_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => path.Substring(SCRATCH_FILES_PATH.Length));
            var original =
                Directory.EnumerateFiles(ORIGINAL_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => path.Substring(ORIGINAL_FILES_PATH.Length));

            Assert.Equal(extracted.Count, original.Count);

            foreach (var orig in original)
            {
                Assert.True(extracted.Contains(orig.Key));

                CompareFilesByPath(orig.Single(), extracted[orig.Key].Single());
            }
        }

        /// <summary>
        /// Verifies the files by name also check modified time and attributes.
        /// </summary>
        protected void VerifyFilesByNameEx()
        {
            var extracted =
                Directory.EnumerateFiles(SCRATCH_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => path.Substring(SCRATCH_FILES_PATH.Length));
            var original =
                Directory.EnumerateFiles(ORIGINAL_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => path.Substring(ORIGINAL_FILES_PATH.Length));

            Assert.Equal(extracted.Count, original.Count);

            foreach (var orig in original)
            {
                Assert.True(extracted.Contains(orig.Key));

                CompareFilesByPath(orig.Single(), extracted[orig.Key].Single());
                CompareFilesByTimeAndAttribut(orig.Single(), extracted[orig.Key].Single());
            }
        }

        /// <summary>
        /// Verifies the files by extension also check modified time and attributes.
        /// </summary>
        protected void VerifyFilesByExtensionEx()
        {
            var extracted =
                Directory.EnumerateFiles(SCRATCH_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => Path.GetExtension(path));
            var original =
                Directory.EnumerateFiles(ORIGINAL_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => Path.GetExtension(path));

            Assert.Equal(extracted.Count, original.Count);

            foreach (var orig in original)
            {
                Assert.True(extracted.Contains(orig.Key));

                CompareFilesByPath(orig.Single(), extracted[orig.Key].Single());
                CompareFilesByTimeAndAttribut(orig.Single(), extracted[orig.Key].Single());
            }
        }

        protected bool UseExtensionInsteadOfNameToVerify { get; set; }

        protected void VerifyFilesByExtension()
        {
            var extracted =
                Directory.EnumerateFiles(SCRATCH_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => Path.GetExtension(path));
            var original =
                Directory.EnumerateFiles(ORIGINAL_FILES_PATH, "*.*", SearchOption.AllDirectories)
                .ToLookup(path => Path.GetExtension(path));

            Assert.Equal(extracted.Count, original.Count);

            foreach (var orig in original)
            {
                Assert.True(extracted.Contains(orig.Key));

                CompareFilesByPath(orig.Single(), extracted[orig.Key].Single());
            }
        }

        protected void CompareFilesByPath(string file1, string file2)
        {
            //TODO: fix line ending issues with the text file
            if (file1.EndsWith("txt"))
            {
                return;
            }

            using (var file1Stream = File.OpenRead(file1))
            using (var file2Stream = File.OpenRead(file2))
            {
                Assert.Equal(file1Stream.Length, file2Stream.Length);
                int byte1 = 0;
                int byte2 = 0;
                for (int counter = 0; byte1 != -1; counter++)
                {
                    byte1 = file1Stream.ReadByte();
                    byte2 = file2Stream.ReadByte();
                    if (byte1 != byte2)
                    {
                        //string.Format("Byte {0} differ between {1} and {2}", counter, file1, file2)
                        Assert.Equal(byte1, byte2);
                    }
                }
            }
        }

        protected void CompareFilesByTimeAndAttribut(string file1, string file2)
        {
            FileInfo fi1 = new FileInfo(file1);
            FileInfo fi2 = new FileInfo(file2);
            Assert.NotEqual(fi1.LastWriteTime, fi2.LastWriteTime);
            Assert.Equal(fi1.Attributes, fi2.Attributes);
        }

        protected void CompareArchivesByPath(string file1, string file2, Encoding encoding = null)
        {
            ReaderOptions readerOptions = new ReaderOptions { LeaveStreamOpen = false };
            readerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

            //don't compare the order.  OS X reads files from the file system in a different order therefore makes the archive ordering different
            var archive1Entries = new List<string>();
            var archive2Entries = new List<string>();
            using (var archive1 = ReaderFactory.Open(File.OpenRead(file1), readerOptions))
            using (var archive2 = ReaderFactory.Open(File.OpenRead(file2), readerOptions))
            {
                while (archive1.MoveToNextEntry())
                {
                    Assert.True(archive2.MoveToNextEntry());
                    archive1Entries.Add(archive1.Entry.Key);
                    archive2Entries.Add(archive2.Entry.Key);
                }
                Assert.False(archive2.MoveToNextEntry());
            }
            archive1Entries.Sort();
            archive2Entries.Sort();
            for (int i = 0; i < archive1Entries.Count; i++)
            {
                Assert.Equal(archive1Entries[i], archive2Entries[i]);
            }
        }

    }
}
