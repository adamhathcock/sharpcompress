using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Test
{
    public class WriterTests : TestBase
    {
        private readonly ArchiveType type;

        protected WriterTests(ArchiveType type)
        {
            this.type = type;
        }

        protected void Write(CompressionType compressionType, string archive, string archiveToVerifyAgainst, Encoding encoding = null)
        {
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            {
                WriterOptions writerOptions = new WriterOptions(compressionType)
                {
                    LeaveStreamOpen = true,
                };

                writerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

                using (var writer = WriterFactory.Open(stream, type, writerOptions))
                {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
            }
            CompareArchivesByPath(Path.Combine(SCRATCH2_FILES_PATH, archive),
               Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst));

            using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            {
                ReaderOptions readerOptions = new ReaderOptions();

                readerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

                using (var reader = ReaderFactory.Open(new NonDisposingStream(stream), readerOptions))
                {
                    reader.WriteAllToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true
                    });
                }
            }
            VerifyFiles();
        }
    }
}
