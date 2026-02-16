using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;

namespace SharpCompress.Test;

public class WriterTests : TestBase
{
    private readonly ArchiveType _type;

    protected WriterTests(ArchiveType type) => _type = type;

    protected void Write(
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

            using var writer = WriterFactory.OpenWriter(stream, _type, writerOptions);
            writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH2_FILES_PATH, archive),
            Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst)
        );

        using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        {
            var readerOptions = new ReaderOptions();

            readerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

            using var reader = ReaderFactory.OpenReader(
                SharpCompressStream.CreateNonDisposing(stream),
                readerOptions
            );
            reader.WriteAllToDirectory(SCRATCH_FILES_PATH);
        }
        VerifyFiles();
    }

    protected async Task WriteAsync(
        CompressionType compressionType,
        string archive,
        string archiveToVerifyAgainst,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default
    )
    {
        using (
            Stream stream = new AsyncOnlyStream(
                File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive))
            )
        )
        {
            var writerOptions = new WriterOptions(compressionType) { LeaveStreamOpen = true };

            writerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

            using var writer = WriterFactory.OpenAsyncWriter(
                stream,
                _type,
                writerOptions,
                cancellationToken
            );
            await writer.WriteAllAsync(
                ORIGINAL_FILES_PATH,
                "*",
                SearchOption.AllDirectories,
                cancellationToken
            );
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH2_FILES_PATH, archive),
            Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst)
        );

        using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        {
            var readerOptions = new ReaderOptions();

            readerOptions.ArchiveEncoding.Default = encoding ?? Encoding.Default;

            await using var reader = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(SharpCompressStream.CreateNonDisposing(stream)),
                readerOptions,
                cancellationToken
            );
            await reader.WriteAllToDirectoryAsync(SCRATCH_FILES_PATH, cancellationToken);
        }
        VerifyFiles();
    }
}
