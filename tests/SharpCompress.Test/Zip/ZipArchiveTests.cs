using System;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip
{
    public class ZipArchiveTests : ArchiveTests
    {
        public ZipArchiveTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void Zip_ZipX_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.zipx");
        }

        [Fact]
        public void Zip_BZip2_Streamed_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.bzip2.dd.zip");
        }
        [Fact]
        public void Zip_BZip2_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.bzip2.zip");
        }
        [Fact]
        public void Zip_Deflate_Streamed2_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.deflate.dd-.zip");
        }
        [Fact]
        public void Zip_Deflate_Streamed_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.deflate.dd.zip");
        }
        [Fact]
        public void Zip_Deflate_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.deflate.zip");
        }
        [Fact]
        public void Zip_Deflate64_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.deflate64.zip");
        }

        [Fact]
        public void Zip_LZMA_Streamed_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.lzma.dd.zip");
        }
        [Fact]
        public void Zip_LZMA_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.lzma.zip");
        }
        [Fact]
        public void Zip_PPMd_Streamed_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.ppmd.dd.zip");
        }
        [Fact]
        public void Zip_PPMd_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.ppmd.zip");
        }
        [Fact]
        public void Zip_None_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.none.zip");
        }

        [Fact]
        public void Zip_BZip2_Streamed_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.bzip2.dd.zip");
        }
        [Fact]
        public void Zip_BZip2_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.bzip2.zip");
        }
        [Fact]
        public void Zip_Deflate_Streamed2_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.deflate.dd-.zip");
        }
        [Fact]
        public void Zip_Deflate_Streamed_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.deflate.dd.zip");
        }
        [Fact]
        public void Zip_Deflate_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.deflate.zip");
        }
        [Fact]
        public void Zip_Deflate64_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.deflate64.zip");
        }

        [Fact]
        public void Zip_LZMA_Streamed_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.lzma.dd.zip");
        }
        [Fact]
        public void Zip_LZMA_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.lzma.zip");
        }
        [Fact]
        public void Zip_PPMd_Streamed_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.ppmd.dd.zip");
        }
        [Fact]
        public void Zip_PPMd_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.ppmd.zip");
        }
        [Fact]
        public void Zip_None_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.none.zip");
        }

        [Fact]
        public void Zip_Zip64_ArchiveStreamRead()
        {
            ArchiveStreamRead("Zip.zip64.zip");
        }

        [Fact]
        public void Zip_Zip64_ArchiveFileRead()
        {
            ArchiveFileRead("Zip.zip64.zip");
        }

        [Fact]
        public void Zip_Random_Write_Remove()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");

            using (var archive = ZipArchive.Open(unmodified))
            {
                var entry = archive.Entries.Single(x => x.Key.EndsWith("jpg"));
                archive.RemoveEntry(entry);

                WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
                writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

                archive.SaveTo(scratchPath, writerOptions);
            }
            CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
        }

        [Fact]
        public void Zip_Random_Write_Add()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod2.zip");

            using (var archive = ZipArchive.Open(unmodified))
            {
                archive.AddEntry("jpg\\test.jpg", jpg);

                WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
                writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

                archive.SaveTo(scratchPath, writerOptions);
            }
            CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
        }

        [Fact]
        public void Zip_Save_Twice()
        {
            string scratchPath1 = Path.Combine(SCRATCH_FILES_PATH, "a.zip");
            string scratchPath2 = Path.Combine(SCRATCH_FILES_PATH, "b.zip");

            using (var arc = ZipArchive.Create())
            {
                string str = "test.txt";
                var source = new MemoryStream(Encoding.UTF8.GetBytes(str));
                arc.AddEntry("test.txt", source, true, source.Length);
                arc.SaveTo(scratchPath1, CompressionType.Deflate);
                arc.SaveTo(scratchPath2, CompressionType.Deflate);
            }

            Assert.Equal(new FileInfo(scratchPath1).Length, new FileInfo(scratchPath2).Length);
        }

        [Fact]
        public void Zip_Removal_Poly()
        {

            string scratchPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");



            using (ZipArchive vfs = (ZipArchive)ArchiveFactory.Open(scratchPath))
            {
                var e = vfs.Entries.First(v => v.Key.EndsWith("jpg"));
                vfs.RemoveEntry(e);
                Assert.Null(vfs.Entries.FirstOrDefault(v => v.Key.EndsWith("jpg")));
                Assert.Null(((IArchive)vfs).Entries.FirstOrDefault(v => v.Key.EndsWith("jpg")));
            }
        }

        [Fact]
        public void Zip_Create_NoDups()
        {
            using (var arc = ZipArchive.Create())
            {
                arc.AddEntry("1.txt", new MemoryStream());
                Assert.Throws<ArchiveException>(() => arc.AddEntry("\\1.txt", new MemoryStream()));
            }
        }

        [Fact]
        public void Zip_Create_Same_Stream()
        {
            string scratchPath1 = Path.Combine(SCRATCH_FILES_PATH, "a.zip");
            string scratchPath2 = Path.Combine(SCRATCH_FILES_PATH, "b.zip");

            using (var arc = ZipArchive.Create())
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("qwert")))
                {
                    arc.AddEntry("1.txt", stream, false, stream.Length);
                    arc.AddEntry("2.txt", stream, false, stream.Length);
                    arc.SaveTo(scratchPath1, CompressionType.Deflate);
                    arc.SaveTo(scratchPath2, CompressionType.Deflate);
                }
            }

            Assert.Equal(new FileInfo(scratchPath1).Length, new FileInfo(scratchPath2).Length);
        }

        [Fact]
        public void Zip_Create_New()
        {
            foreach (var file in Directory.EnumerateFiles(ORIGINAL_FILES_PATH, "*.*", SearchOption.AllDirectories))
            {
                var newFileName = file.Substring(ORIGINAL_FILES_PATH.Length);
                if (newFileName.StartsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    newFileName = newFileName.Substring(1);
                }
                newFileName = Path.Combine(SCRATCH_FILES_PATH, newFileName);
                var newDir = Path.GetDirectoryName(newFileName);
                if (!Directory.Exists(newDir))
                {
                    Directory.CreateDirectory(newDir);
                }
                File.Copy(file, newFileName);
            }
            string scratchPath = Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

            using (var archive = ZipArchive.Create())
            {
                archive.AddAllFromDirectory(SCRATCH_FILES_PATH);

                WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
                writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

                archive.SaveTo(scratchPath, writerOptions);
            }
            CompareArchivesByPath(unmodified, scratchPath, Encoding.GetEncoding(866));
            Directory.Delete(SCRATCH_FILES_PATH, true);
        }

        [Fact]
        public void Zip_Create_New_Add_Remove()
        {
            foreach (var file in Directory.EnumerateFiles(ORIGINAL_FILES_PATH, "*.*", SearchOption.AllDirectories))
            {
                var newFileName = file.Substring(ORIGINAL_FILES_PATH.Length);
                if (newFileName.StartsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    newFileName = newFileName.Substring(1);
                }
                newFileName = Path.Combine(SCRATCH_FILES_PATH, newFileName);
                var newDir = Path.GetDirectoryName(newFileName);
                if (!Directory.Exists(newDir))
                {
                    Directory.CreateDirectory(newDir);
                }
                File.Copy(file, newFileName);
            }
            string scratchPath = Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip");

            using (var archive = ZipArchive.Create())
            {
                archive.AddAllFromDirectory(SCRATCH_FILES_PATH);
                archive.RemoveEntry(archive.Entries.Single(x => x.Key.EndsWith("jpg", StringComparison.OrdinalIgnoreCase)));
                Assert.Null(archive.Entries.FirstOrDefault(x => x.Key.EndsWith("jpg")));
            }
            Directory.Delete(SCRATCH_FILES_PATH, true);
        }

        [Fact]
        public void Zip_Deflate_WinzipAES_Read()
        {
            using (var reader = ZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip"), new ReaderOptions()
            {
                Password = "test"
            }))
            {
                foreach (var entry in reader.Entries.Where(x => !x.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Zip_Deflate_WinzipAES_MultiOpenEntryStream()
        {
            using (var reader = ZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES2.zip"), new ReaderOptions()
            {
                Password = "test"
            }))
            {
                foreach (var entry in reader.Entries.Where(x => !x.IsDirectory))
                {
                    var stream = entry.OpenEntryStream();
                    Assert.NotNull(stream);
                    var ex = Record.Exception(() => stream = entry.OpenEntryStream());
                    Assert.Null(ex);
                }
            }
        }

        [Fact]
        public void Zip_Read_Volume_Comment()
        {
            using (var reader = ZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Zip.zip64.zip"), new ReaderOptions()
            {
                Password = "test"
            }))
            {
                var isComplete = reader.IsComplete;
                Assert.Equal(1, reader.Volumes.Count);

                string expectedComment = "Encoding:utf-8 || Compression:Deflate levelDefault || Encrypt:None || ZIP64:Always\r\nCreated at 2017-Jan-23 14:10:43 || DotNetZip Tool v1.9.1.8\r\nTest zip64 archive";
                Assert.Equal(expectedComment, reader.Volumes.First().Comment);
            }
        }

        [Fact]
        public void Zip_BZip2_Pkware_Read()
        {
            using (var reader = ZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip"), new ReaderOptions()
            {
                Password = "test"
            }))
            {
                foreach (var entry in reader.Entries.Where(x => !x.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Zip_Random_Entry_Access()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

            ZipArchive a = ZipArchive.Open(unmodified);
            int count = 0;
            foreach (var e in a.Entries)
            {
                count++;
            }

            //Prints 3
            Assert.Equal(3, count);
            a.Dispose();

            a = ZipArchive.Open(unmodified);
            int count2 = 0;

            foreach (var e in a.Entries)
            {
                count2++;

                //Stop at last file
                if (count2 == count)
                {
                    var s = e.OpenEntryStream();
                    s.ReadByte(); //Actually access stream
                    s.Dispose();
                    break;
                }
            }

            int count3 = 0;
            foreach (var e in a.Entries)
            {
                count3++;
            }

            Assert.Equal(3, count3);
        }

        [Fact]
        public void Zip_Deflate_PKWear_Multipy_Entry_Access()
        {
            string zipFile = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.pkware.zip");

            using (FileStream fileStream = File.Open(zipFile, FileMode.Open))
            {
                using (IArchive archive = ArchiveFactory.Open(fileStream, new ReaderOptions { Password = "12345678" }))
                {
                    var entries = archive.Entries.Where(entry => !entry.IsDirectory);
                    foreach (IArchiveEntry entry in entries)
                    {
                        for (var i = 0; i < 100; i++)
                        {
                            using (var memoryStream = new MemoryStream())
                            using (Stream entryStream = entry.OpenEntryStream())
                            {
                                entryStream.CopyTo(memoryStream);
                            }
                        }
                    }
                }
            }
        }

        [SkippableFact]
        public void Zip_Evil_Throws_Exception()
        {
            //windows only because of the paths
            Skip.IfNot(Environment.OSVersion.Platform == PlatformID.Win32NT);

            string zipFile = Path.Combine(TEST_ARCHIVES_PATH, "Zip.Evil.zip");

            Assert.ThrowsAny<Exception>(() =>
            {
                using (var archive = ZipArchive.Open(zipFile))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            });
        }

        private class NonSeekableMemoryStream : MemoryStream
        {
            public override bool CanSeek => false;
        }

        [Fact]
        public void TestSharpCompressWithEmptyStream()
        {
            MemoryStream stream = new NonSeekableMemoryStream();

            using (IWriter zipWriter = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
            {
                zipWriter.Write("foo.txt", new MemoryStream(new byte[0]));
                zipWriter.Write("foo2.txt", new MemoryStream(new byte[10]));
            }

            stream = new MemoryStream(stream.ToArray());
            File.WriteAllBytes(Path.Combine(SCRATCH_FILES_PATH, "foo.zip"), stream.ToArray());

            using (var zipArchive = ZipArchive.Open(stream))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        MemoryStream tempStream = new MemoryStream();
                        const int bufSize = 0x1000;
                        byte[] buf = new byte[bufSize];
                        int bytesRead = 0;
                        while ((bytesRead = entryStream.Read(buf, 0, bufSize)) > 0)
                        {
                            tempStream.Write(buf, 0, bytesRead);
                        }
                    }
                }
            }
        }

        [Fact]
        public void Zip_BadLocalExtra_Read()
        {
            string zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.badlocalextra.zip");

            using (ZipArchive za = ZipArchive.Open(zipPath))
            {
                var ex = Record.Exception(() =>
                {
                    var firstEntry = za.Entries.First(x => x.Key == "first.txt");
                    var buffer = new byte[4096];

                    using (var memoryStream = new MemoryStream())
                    using (var firstStream = firstEntry.OpenEntryStream())
                    {
                        firstStream.CopyTo(memoryStream);
                        Assert.Equal(199, memoryStream.Length);
                    }
                });

                Assert.Null(ex);
            }
        }

        [Fact]
        public void Zip_NoCompression_DataDescriptors_Read()
        {
            string zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.datadescriptors.zip");

            using (ZipArchive za = ZipArchive.Open(zipPath))
            {
                var firstEntry = za.Entries.First(x => x.Key == "first.txt");
                var buffer = new byte[4096];

                using (var memoryStream = new MemoryStream())
                using (var firstStream = firstEntry.OpenEntryStream())
                {
                    firstStream.CopyTo(memoryStream);
                    Assert.Equal(199, memoryStream.Length);
                }
            }
        }

        [Fact]
        public void Zip_LongComment_Read()
        {
            string zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.LongComment.zip");

            using(ZipArchive za = ZipArchive.Open(zipPath))
            {
                var count = za.Entries.Count;
                Assert.Equal(1, count);
            }
        }

        [Fact]
        public void Zip_Zip64_CompressedSizeExtraOnly_Read()
        {
            string zipPath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.zip64.compressedonly.zip");

            using (ZipArchive za = ZipArchive.Open(zipPath))
            {
                var firstEntry = za.Entries.First(x => x.Key == "test/test.txt");

                using (var memoryStream = new MemoryStream())
                using (var firstStream = firstEntry.OpenEntryStream())
                {
                    firstStream.CopyTo(memoryStream);
                    Assert.Equal(15, memoryStream.Length);
                }
            }
        }
    }
}
