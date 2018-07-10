using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Tar
{
    public class TarReaderTests : ReaderTests
    {
        public TarReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void Tar_Reader()
        {
            Read("Tar.tar", CompressionType.None);
        }

        [Fact]
        public void Tar_Skip()
        {
            using (Stream stream = new ForwardOnlyStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"))))
            using (IReader reader = ReaderFactory.Open(stream))
            {
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
        public void Tar_BZip2_Reader()
        {
            Read("Tar.tar.bz2", CompressionType.BZip2);
        }

        [Fact]
        public void Tar_GZip_Reader()
        {
            Read("Tar.tar.gz", CompressionType.GZip);
        }

        [Fact]
        public void Tar_LZip_Reader()
        {
            Read("Tar.tar.lz", CompressionType.LZip);
        }

        [Fact]
        public void Tar_Xz_Reader()
        {
            Read("Tar.tar.xz", CompressionType.Xz);
        }

        [Fact]
        public void Tar_BZip2_Entry_Stream()
        {
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2")))
            using (var reader = TarReader.Open(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                        using (var entryStream = reader.OpenEntryStream())
                        {
                            string file = Path.GetFileName(reader.Entry.Key);
                            string folder = Path.GetDirectoryName(reader.Entry.Key);
                            string destdir = Path.Combine(SCRATCH_FILES_PATH, folder);
                            if (!Directory.Exists(destdir))
                            {
                                Directory.CreateDirectory(destdir);
                            }
                            string destinationFileName = Path.Combine(destdir, file);

                            using (FileStream fs = File.OpenWrite(destinationFileName))
                            {
                                entryStream.TransferTo(fs);
                            }
                        }
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Tar_LongNamesWithLongNameExtension()
        {
            var filePaths = new List<string>();

            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.LongPathsWithLongNameExtension.tar")))
            using (var reader = TarReader.Open(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        filePaths.Add(reader.Entry.Key);
                    }
                }                
            }

            Assert.Equal(3, filePaths.Count);
            Assert.Contains("a.txt", filePaths);
            Assert.Contains("wp-content/plugins/gravityformsextend/lib/Aws/Symfony/Component/ClassLoader/Tests/Fixtures/Apc/beta/Apc/ApcPrefixCollision/A/B/Bar.php", filePaths);
            Assert.Contains("wp-content/plugins/gravityformsextend/lib/Aws/Symfony/Component/ClassLoader/Tests/Fixtures/Apc/beta/Apc/ApcPrefixCollision/A/B/Foo.php", filePaths);
        }

        [Fact]
        public void Tar_BZip2_Skip_Entry_Stream()
        {
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2")))
            using (var reader = TarReader.Open(stream))
            {
                List<string> names = new List<string>();
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                        using (var entryStream = reader.OpenEntryStream())
                        {
                            entryStream.SkipEntry();
                            names.Add(reader.Entry.Key);
                        }
                    }
                }
                Assert.Equal(3, names.Count);
            }
        }

        [Fact]
        public void Tar_Containing_Rar_Reader()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
            using (Stream stream = File.OpenRead(archiveFullPath))
            using (IReader reader = ReaderFactory.Open(stream))
            {
                Assert.True(reader.ArchiveType == ArchiveType.Tar);
            }
        }
    }
}
