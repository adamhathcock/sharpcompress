using System.IO;
using System.Text;
using System.Threading.Tasks;
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

        protected async Task WriteAsync(CompressionType compressionType, string archive, string archiveToVerifyAgainst, Encoding encoding = null)
        {
            await using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            {
                WriterOptions writerOptions = new WriterOptions(compressionType)
                {
                    LeaveStreamOpen = true,
                };

                writerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

                await using (var writer = await WriterFactory.OpenAsync(stream, type, writerOptions))
                {
                    await writer.WriteAllAsync(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
            }
            await CompareArchivesByPathAsync(Path.Combine(SCRATCH2_FILES_PATH, archive),
                                       Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst));

            await using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            {
                ReaderOptions readerOptions = new ReaderOptions();

                readerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

                await using (var reader = await ReaderFactory.OpenAsync(new NonDisposingStream(stream), readerOptions))
                {
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true
                    });
                }
            }
            VerifyFiles();
        }
    }
}
