using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.PlatformAbstractions;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test
{
    public class TestBase : IDisposable
    {
        protected string SOLUTION_BASE_PATH=null;
        protected string TEST_ARCHIVES_PATH;
        protected string ORIGINAL_FILES_PATH;
        protected string MISC_TEST_FILES_PATH;
        public string SCRATCH_FILES_PATH;
        protected string SCRATCH2_FILES_PATH;
        protected IEnumerable<string> GetRarArchives()
        {
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Rar.none.rar");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Rar.solid.rar");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part01.rar");
        }
        protected IEnumerable<string> GetZipArchives()
        {
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.dd.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd-.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.lzma.dd.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.lzma.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.ppmd.dd.zip");
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Zip.ppmd.zip");
        }
        protected IEnumerable<string> GetTarArchives()
        {
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        }
        protected IEnumerable<string> GetTarBz2Archives()
        {
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2");
        }
        protected IEnumerable<string> GetTarGzArchives()
        {
            yield return Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
        }

        public void ResetScratch()
        {
            if (Directory.Exists(SCRATCH_FILES_PATH))
            {
                Directory.Delete(SCRATCH_FILES_PATH, true);
            }
            Directory.CreateDirectory(SCRATCH_FILES_PATH);
            if (Directory.Exists(SCRATCH2_FILES_PATH))
            {
                Directory.Delete(SCRATCH2_FILES_PATH, true);
            }
            Directory.CreateDirectory(SCRATCH2_FILES_PATH);

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

        protected void CompareArchivesByPath(string file1, string file2)
        {
            //don't compare the order.  OS X reads files from the file system in a different order therefore makes the archive ordering different
            var archive1Entries = new List<string>();
            var archive2Entries = new List<string>();
            using (var archive1 = ReaderFactory.Open(File.OpenRead(file1)))
            using (var archive2 = ReaderFactory.Open(File.OpenRead(file2)))
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

        private static readonly object lockObject = new object();

        public TestBase()
        {
            Monitor.Enter(lockObject);
            var index = PlatformServices.Default.Application.ApplicationBasePath.IndexOf("SharpCompress.Test", StringComparison.OrdinalIgnoreCase);
            SOLUTION_BASE_PATH = Path.GetDirectoryName(PlatformServices.Default.Application.ApplicationBasePath.Substring(0, index));
            TEST_ARCHIVES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Archives");
            ORIGINAL_FILES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Original");
            MISC_TEST_FILES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "MiscTest");
            SCRATCH_FILES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Scratch");
            SCRATCH2_FILES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Scratch2");
        }

        public void Dispose()
        {
            Monitor.Exit(lockObject);
        }
    }
}
