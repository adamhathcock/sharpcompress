using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Reader.Zip;
using SharpCompress.Writer;

namespace SharpCompress.Test
{
    [TestClass]
    public class ZipReaderTests : ReaderTests
    {
        public ZipReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }
        [TestMethod]
        public void Zip_ZipX_Streamed_Read()
        {
            Read("Zip.Zipx", CompressionType.LZMA);
        }

        [TestMethod]
        public void Zip_BZip2_Streamed_Read()
        {
            Read("Zip.bzip2.dd.zip", CompressionType.BZip2);
        }
        [TestMethod]
        public void Zip_BZip2_Read()
        {
            Read("Zip.bzip2.zip", CompressionType.BZip2);
        }
        [TestMethod]
        public void Zip_Deflate_Streamed2_Read()
        {
            Read("Zip.deflate.dd-.zip", CompressionType.Deflate);
        }
        [TestMethod]
        public void Zip_Deflate_Streamed_Read()
        {
            Read("Zip.deflate.dd.zip", CompressionType.Deflate);
        }
        [TestMethod]
        public void Zip_Deflate_Read()
        {
            Read("Zip.deflate.zip", CompressionType.Deflate);
        }

        [TestMethod]
        public void Zip_LZMA_Streamed_Read()
        {
            Read("Zip.lzma.dd.zip", CompressionType.LZMA);
        }
        [TestMethod]
        public void Zip_LZMA_Read()
        {
            Read("Zip.lzma.zip", CompressionType.LZMA);
        }
        [TestMethod]
        public void Zip_PPMd_Streamed_Read()
        {
            Read("Zip.ppmd.dd.zip", CompressionType.PPMd);
        }
        [TestMethod]
        public void Zip_PPMd_Read()
        {
            Read("Zip.ppmd.zip", CompressionType.PPMd);
        }

        [TestMethod]
        public void Zip_None_Read()
        {
            Read("Zip.none.zip", CompressionType.None);
        }

        [TestMethod]
        public void Zip_Deflate_NoEmptyDirs_Read()
        {
            Read("Zip.deflate.noEmptyDirs.zip", CompressionType.Deflate);
        }

        [TestMethod]
        public void Zip_BZip2_PkwareEncryption_Read()
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip")))
            using (var reader = ZipReader.Open(stream, "test"))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.AreEqual(reader.Entry.CompressionType, CompressionType.BZip2);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
            VerifyFiles();
        }

        [TestMethod]
        public void Zip_Reader_Disposal_Test()
        {
            ResetScratch();
            using (TestStream stream = new TestStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            {
                using (var reader = ReaderFactory.Open(stream, Options.None))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                         ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                        }
                    }
                }
                Assert.IsTrue(stream.IsDisposed);
            }
        }

        [TestMethod]
        public void Zip_Reader_Disposal_Test2()
        {
            ResetScratch();
            using (TestStream stream = new TestStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            {
                var reader = ReaderFactory.Open(stream, Options.None);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                     ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
                Assert.IsFalse(stream.IsDisposed);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void Zip_LZMA_WinzipAES_Read()
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.lzma.winzipaes.zip")))
            using (var reader = ZipReader.Open(stream, "test"))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Unknown);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
            VerifyFiles();
        }

        [TestMethod]
        public void Zip_Deflate_WinzipAES_Read()
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip")))
            using (var reader = ZipReader.Open(stream, "test"))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Unknown);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                     ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
            VerifyFiles();
        }

        class NonSeekableMemoryStream : MemoryStream
        {
            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }
        }

        [TestMethod]
        public void TestSharpCompressWithEmptyStream()
        {
            MemoryStream stream = new NonSeekableMemoryStream();

            using (IWriter zipWriter = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
            {
                zipWriter.Write("foo.txt", new MemoryStream(new byte[0]));
                zipWriter.Write("foo2.txt", new MemoryStream(new byte[10]));
            }

            stream = new MemoryStream(stream.ToArray());
            File.WriteAllBytes("foo.zip", stream.ToArray());

            using (IReader zipReader = ZipReader.Open(stream))
            {
                while (zipReader.MoveToNextEntry())
                {
                    using (EntryStream entry = zipReader.OpenEntryStream())
                    {
                        MemoryStream tempStream = new MemoryStream();
                        const int bufSize = 0x1000;
                        byte[] buf = new byte[bufSize];
                        int bytesRead = 0;
                        while ((bytesRead = entry.Read(buf, 0, bufSize)) > 0)
                            tempStream.Write(buf, 0, bytesRead);
                    }
                }
            }
        }
    }
}
