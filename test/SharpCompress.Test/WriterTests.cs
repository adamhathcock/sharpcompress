using System.IO;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Writer;

namespace SharpCompress.Test
{
    public class WriterTests : TestBase
    {
        private ArchiveType type;

        protected WriterTests(ArchiveType type)
        {
            this.type = type;
        }

        protected void Write(CompressionType compressionType, string archive, string archiveToVerifyAgainst)
        {
            ResetScratch();
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            using (var writer = WriterFactory.Open(stream, type, compressionType))
            {
               writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
            }
            CompareArchivesByPath(Path.Combine(SCRATCH2_FILES_PATH, archive),
               Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst));

            using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            using (var reader = ReaderFactory.Open(stream))
            {
               reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath);
            }
            VerifyFiles();
        }
    }
}
