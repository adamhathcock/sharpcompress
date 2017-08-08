using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip
{
    public class ZipWriterTests : WriterTests
    {
        public ZipWriterTests()
            : base(ArchiveType.Zip)
        {
        }

        [Fact]
        public void Zip_Deflate_Write()
        {
            Write(CompressionType.Deflate, "Zip.deflate.noEmptyDirs.zip", "Zip.deflate.noEmptyDirs.zip");
        }


        [Fact]
        public void Zip_BZip2_Write()
        {
            Write(CompressionType.BZip2, "Zip.bzip2.noEmptyDirs.zip", "Zip.bzip2.noEmptyDirs.zip");
        }


        [Fact]
        public void Zip_None_Write()
        {
            Write(CompressionType.None, "Zip.none.noEmptyDirs.zip", "Zip.none.noEmptyDirs.zip");
        }


        [Fact]
        public void Zip_LZMA_Write()
        {
            Write(CompressionType.LZMA, "Zip.lzma.noEmptyDirs.zip", "Zip.lzma.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_PPMd_Write()
        {
            Write(CompressionType.PPMd, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_Rar_Write()
        {
            Assert.Throws<InvalidFormatException>(() => Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip"));
        }
        
        [Fact]
        public void Zip_BZip2_PkwareEncryption_Write()
        {
            ResetScratch();
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH_FILES_PATH, "Zip.pkware.zip")))
            {
                using (var writer = new ZipWriter(stream, new ZipWriterOptions(CompressionType.BZip2)
                                                          {
                                                              Password = "test"
                                                          }))
                {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }  
            }
            using (Stream stream = File.OpenRead(Path.Combine(SCRATCH_FILES_PATH, "Zip.pkware.zip")))
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
    }
}
