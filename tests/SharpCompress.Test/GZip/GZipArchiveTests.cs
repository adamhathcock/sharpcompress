using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using Xunit;

namespace SharpCompress.Test.GZip
{
    public class GZipArchiveTests : ArchiveTests
    {
        public GZipArchiveTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public async ValueTask GZip_Archive_Generic()
        {
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
            using (var archive = await ArchiveFactory.OpenAsync(stream))
            {
                var entry = archive.Entries.First();
                await entry.WriteToFileAsync(Path.Combine(SCRATCH_FILES_PATH, entry.Key));

                long size = entry.Size;
                var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
                var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

                Assert.Equal(size, scratch.Length);
                Assert.Equal(size, test.Length);
            }
            await CompareArchivesByPathAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
                                       Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }

        [Fact]
        public async ValueTask GZip_Archive()
        {
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
            using (var archive = GZipArchive.Open(stream))
            {
                var entry = archive.Entries.First();
                await entry.WriteToFileAsync(Path.Combine(SCRATCH_FILES_PATH, entry.Key));

                long size = entry.Size;
                var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
                var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

                Assert.Equal(size, scratch.Length);
                Assert.Equal(size, test.Length);
            }
            await CompareArchivesByPathAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
                                       Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }


        [Fact]
        public async Task GZip_Archive_NoAdd()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
            using (var archive = GZipArchive.Open(stream))
            {
                Assert.Throws<InvalidOperationException>(() => archive.AddEntry("jpg\\test.jpg", jpg));
                await archive.SaveToAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"));
            }
        }


        [Fact]
        public void GZip_Archive_Multiple_Reads()
        {
            var inputStream = new MemoryStream();
            using (var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
            {
                fileStream.CopyTo(inputStream);
                inputStream.Position = 0;
            }
            using (var archive = GZipArchive.Open(inputStream))
            {
                var archiveEntry = archive.Entries.First();

                MemoryStream tarStream;
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    tarStream = new MemoryStream();
                    entryStream.CopyTo(tarStream);
                }
                var size = tarStream.Length;
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    tarStream = new MemoryStream();
                    entryStream.CopyTo(tarStream);
                }
                Assert.Equal(size, tarStream.Length);
                /*using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    var result = Archives.Tar.TarArchive.IsTarFile(entryStream);
                }
                Assert.Equal(size, tarStream.Length);           */
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    tarStream = new MemoryStream();
                    entryStream.CopyTo(tarStream);
                }
                Assert.Equal(size, tarStream.Length);
            }
        }
    }
}
