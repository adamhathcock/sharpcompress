using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Zip
{
    public class ZipReaderTests : ReaderTests
    {
        public ZipReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }
        
        [Fact]
        public void Issue_269_Double_Skip()
        {
            ResetScratch();
            var path = Path.Combine(TEST_ARCHIVES_PATH, "PrePostHeaders.zip");
            using (Stream stream = new ForwardOnlyStream(File.OpenRead(path)))
            using (IReader reader = ReaderFactory.Open(stream))
            {
                int count = 0;
                while (reader.MoveToNextEntry())
                {
                    count++;
                    if (!reader.Entry.IsDirectory)
                    {
                        if (count % 2 != 0)
                        {
                            reader.WriteEntryTo(Stream.Null);
                        }
                    }
                }
            }
        }

        [Fact]
        public void Zip_Zip64_Streamed_Read()
        {
            Read("Zip.Zip64.zip", CompressionType.Deflate);
        }

        [Fact]
        public void Zip_ZipX_Streamed_Read()
        {
            Read("Zip.Zipx", CompressionType.LZMA);
        }

        [Fact]
        public void Zip_BZip2_Streamed_Read()
        {
            Read("Zip.bzip2.dd.zip", CompressionType.BZip2);
        }
        [Fact]
        public void Zip_BZip2_Read()
        {
            Read("Zip.bzip2.zip", CompressionType.BZip2);
        }
        [Fact]
        public void Zip_Deflate_Streamed2_Read()
        {
            Read("Zip.deflate.dd-.zip", CompressionType.Deflate);
        }
        [Fact]
        public void Zip_Deflate_Streamed_Read()
        {
            Read("Zip.deflate.dd.zip", CompressionType.Deflate);
        }
        [Fact]
        public void Zip_Deflate_Streamed_Skip()
        {
            using (Stream stream = new ForwardOnlyStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            using (IReader reader = ReaderFactory.Open(stream))
            {
                ResetScratch();
                int x = 0;
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        x++;
                        if (x % 2 == 0)
                        {
                            reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                         new ExtractionOptions()
                                                         {
                                                             ExtractFullPath = true,
                                                             Overwrite = true
                                                         });
                        }
                    }
                }
            }
        }
        [Fact]
        public void Zip_Deflate_Read()
        {
            Read("Zip.deflate.zip", CompressionType.Deflate);
        }

        [Fact]
        public void Zip_LZMA_Streamed_Read()
        {
            Read("Zip.lzma.dd.zip", CompressionType.LZMA);
        }
        [Fact]
        public void Zip_LZMA_Read()
        {
            Read("Zip.lzma.zip", CompressionType.LZMA);
        }
        [Fact]
        public void Zip_PPMd_Streamed_Read()
        {
            Read("Zip.ppmd.dd.zip", CompressionType.PPMd);
        }
        [Fact]
        public void Zip_PPMd_Read()
        {
            Read("Zip.ppmd.zip", CompressionType.PPMd);
        }

        [Fact]
        public void Zip_None_Read()
        {
            Read("Zip.none.zip", CompressionType.None);
        }

        [Fact]
        public void Zip_Deflate_NoEmptyDirs_Read()
        {
            Read("Zip.deflate.noEmptyDirs.zip", CompressionType.Deflate);
        }

        [Fact]
        public void Zip_BZip2_PkwareEncryption_Read()
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip")))
            using (var reader = ZipReader.Open(stream, new ReaderOptions()
                                                       {
                                                           Password = "test"
            }))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Zip_Reader_Disposal_Test()
        {
            ResetScratch();
            using (TestStream stream = new TestStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            {
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                         new ExtractionOptions()
                                                         {
                                                             ExtractFullPath = true,
                                                             Overwrite = true
                                                         });
                        }
                    }
                }
                Assert.True(stream.IsDisposed);
            }
        }

        [Fact]
        public void Zip_Reader_Disposal_Test2()
        {
            ResetScratch();
            using (TestStream stream = new TestStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            {
                var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                     new ExtractionOptions()
                                                     {
                                                         ExtractFullPath = true,
                                                         Overwrite = true
                                                     });
                    }
                }
                Assert.False(stream.IsDisposed);
            }
        }

        [Fact]
        public void Zip_LZMA_WinzipAES_Read()
        {
            Assert.Throws<NotSupportedException>(() =>
                                            {
                                                ResetScratch();
                                                using (
                                                    Stream stream =
                                                        File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH,
                                                            "Zip.lzma.winzipaes.zip")))
                                                using (var reader = ZipReader.Open(stream, new ReaderOptions()
                                                                                           {
                                                                                               Password = "test"
                                                                                           }))
                                                {
                                                    while (reader.MoveToNextEntry())
                                                    {
                                                        if (!reader.Entry.IsDirectory)
                                                        {
                                                            Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                                                            reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                                new ExtractionOptions()
                                                                {
                                                                    ExtractFullPath = true,
                                                                    Overwrite = true
                                                                });
                                                        }
                                                    }
                                                }
                                                VerifyFiles();
                                            });
        }

        [Fact]
        public void Zip_Deflate_WinzipAES_Read()
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip")))
            using (var reader = ZipReader.Open(stream, new ReaderOptions()
                                                       {
                                                           Password = "test"
                                                       }))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                    new ExtractionOptions()
                                                    {
                                                        ExtractFullPath = true,
                                                        Overwrite = true
                                                    });
                    }
                }
            }
            VerifyFiles();
        }

        class NonSeekableMemoryStream : MemoryStream
        {
            public override bool CanSeek => false;
        }

        [Fact]
        public void TestSharpCompressWithEmptyStream()
        {
            ResetScratch();

            MemoryStream stream = new NonSeekableMemoryStream();

            using (IWriter zipWriter = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
            {
                zipWriter.Write("foo.txt", new MemoryStream(new byte[0]));
                zipWriter.Write("foo2.txt", new MemoryStream(new byte[10]));
            }

            stream = new MemoryStream(stream.ToArray());
            File.WriteAllBytes(Path.Combine(SCRATCH_FILES_PATH, "foo.zip"), stream.ToArray());

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
                        {
                            tempStream.Write(buf, 0, bytesRead);
                        }
                    }
                }
            }
        }
    }
}
