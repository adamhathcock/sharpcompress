using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        public async ValueTask Tar_Reader()
        {
            await ReadAsync("Tar.tar", CompressionType.None);
        }

        [Fact]
        public async ValueTask Tar_Skip()
        {
            await using (Stream stream = new ForwardOnlyStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"))))
            await using (IReader reader = await ReaderFactory.OpenAsync(stream))
            {
                int x = 0;
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        x++;
                        if (x % 2 == 0)
                        {
                            await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
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
        public async ValueTask Tar_BZip2_Reader()
        {
            await ReadAsync("Tar.tar.bz2", CompressionType.BZip2);
        }

        [Fact]
        public async ValueTask Tar_GZip_Reader()
        {
            await ReadAsync("Tar.tar.gz", CompressionType.GZip);
        }

        [Fact]
        public async ValueTask Tar_LZip_Reader()
        {
            await ReadAsync("Tar.tar.lz", CompressionType.LZip);
        }

        [Fact]
        public async ValueTask Tar_Xz_Reader()
        {
            await ReadAsync("Tar.tar.xz", CompressionType.Xz);
        }

        [Fact]
        public async ValueTask Tar_BZip2_Entry_Stream()
        {
            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2")))
            await using (var reader = await TarReader.OpenAsync(stream))
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                        await using (var entryStream = await reader.OpenEntryStreamAsync())
                        {
                            string file = Path.GetFileName(reader.Entry.Key);
                            string folder = Path.GetDirectoryName(reader.Entry.Key);
                            string destdir = Path.Combine(SCRATCH_FILES_PATH, folder);
                            if (!Directory.Exists(destdir))
                            {
                                Directory.CreateDirectory(destdir);
                            }
                            string destinationFileName = Path.Combine(destdir, file);

                            await using (FileStream fs = File.OpenWrite(destinationFileName))
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
        public async ValueTask Tar_LongNamesWithLongNameExtension()
        {
            var filePaths = new List<string>();

            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.LongPathsWithLongNameExtension.tar")))
            await using (var reader = await TarReader.OpenAsync(stream))
            {
                while (await reader.MoveToNextEntryAsync())
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
        public async ValueTask Tar_BZip2_Skip_Entry_Stream()
        {
            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.bz2")))
            await using (var reader = await TarReader.OpenAsync(stream))
            {
                List<string> names = new List<string>();
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                        await using (var entryStream = await reader.OpenEntryStreamAsync())
                        {
                            await entryStream.SkipEntryAsync();
                            names.Add(reader.Entry.Key);
                        }
                    }
                }
                Assert.Equal(3, names.Count);
            }
        }

        [Fact]
        public async ValueTask Tar_Containing_Rar_Reader()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
            await using (Stream stream = File.OpenRead(archiveFullPath))
            await using (IReader reader = await ReaderFactory.OpenAsync(stream))
            {
                Assert.True(reader.ArchiveType == ArchiveType.Tar);
            }
        }

        [Fact]
        public async ValueTask Tar_With_TarGz_With_Flushed_EntryStream()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsTarGz.tar");
            await using (Stream stream = File.OpenRead(archiveFullPath))
            await using (IReader reader = await ReaderFactory.OpenAsync(stream))
            {
                Assert.True(await reader.MoveToNextEntryAsync());
                Assert.Equal("inner.tar.gz", reader.Entry.Key);

                await using (var entryStream = await reader.OpenEntryStreamAsync())
                {
                    await using (FlushOnDisposeStream flushingStream = new FlushOnDisposeStream(entryStream))
                    {

                        // Extract inner.tar.gz
                        await using (var innerReader = await ReaderFactory.OpenAsync(flushingStream))
                        {

                            Assert.True(await innerReader.MoveToNextEntryAsync());
                            Assert.Equal("test", innerReader.Entry.Key);

                        }
                    }
                }
            }
        }

#if !NET461
        [Fact]
        public async ValueTask Tar_GZip_With_Symlink_Entries()
        {
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);
            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "TarWithSymlink.tar.gz")))
            await using (var reader = await TarReader.OpenAsync(stream))
            {
                List<string> names = new List<string>();
                while (await reader.MoveToNextEntryAsync())
                {
                    if (reader.Entry.IsDirectory)
                    {
                        continue;
                    }
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                 new ExtractionOptions()
                                                 {
                                                     ExtractFullPath = true,
                                                     Overwrite = true,
                                                     WriteSymbolicLink = (sourcePath, targetPath) =>
                                                     {
                                                         if (!isWindows)
                                                         {
                                                             var link = new Mono.Unix.UnixSymbolicLinkInfo(sourcePath);
                                                             if (File.Exists(sourcePath))
                                                             {
                                                                 link.Delete(); // equivalent to ln -s -f
                                                             }
                                                             link.CreateSymbolicLinkTo(targetPath);
                                                         }
                                                     }
                                                 });
                    if (!isWindows)
                    {
                        if (reader.Entry.LinkTarget != null)
                        {
                            var path = System.IO.Path.Combine(SCRATCH_FILES_PATH, reader.Entry.Key);
                            var link = new Mono.Unix.UnixSymbolicLinkInfo(path);
                            if (link.HasContents)
                            {
                                // need to convert the link to an absolute path for comparison
                                var target = reader.Entry.LinkTarget;
                                var realTarget = System.IO.Path.GetFullPath(
                                    System.IO.Path.Combine($"{System.IO.Path.GetDirectoryName(path)}",
                                    target)
                                );

                                Assert.Equal(realTarget, link.GetContents().ToString());
                            }
                            else
                            {
                                Assert.True(false, "Symlink has no target");
                            }
                        }
                    }
                }
            }
        }
#endif

    }
}
