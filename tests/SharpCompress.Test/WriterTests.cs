using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Test;

public class WriterTests : TestBase
{
    private readonly ArchiveType _type;

    protected WriterTests(ArchiveType type) => _type = type;

    protected async Task WriteAsync(
        CompressionType compressionType,
        string archive,
        string archiveToVerifyAgainst,
        Encoding? encoding = null
    )
    {
        using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        {
            var writerOptions = new WriterOptions(compressionType) { LeaveStreamOpen = true };

            writerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

            using var writer = WriterFactory.Open(stream, _type, writerOptions);
            writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
        }

        await CompareArchivesByPathAsync(
            Path.Combine(SCRATCH2_FILES_PATH, archive),
            Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst)
        );

        using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        {
            var readerOptions = new ReaderOptions();

            readerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

            using var reader = ReaderFactory.Open(
                SharpCompressStream.Create(stream, leaveOpen: true),
                readerOptions
            );
            await reader.WriteAllToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true }
            );
        }
        VerifyFiles();
    }
}
