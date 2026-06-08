using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipArchiveAsyncParallelExtractionTests : TestBase
{
    [Fact]
    public async ValueTask RequireParallel_DetectsDuplicateOutputPathsBeforeWriting()
    {
        var archivePath = GetScratch2Path("duplicate-output.zip");
        CreateZip(archivePath, ("duplicate.txt", "first"), ("duplicate.txt", "second"));

        var exception = await Assert.ThrowsAsync<ExtractionException>(async () =>
            await ArchiveFactory.ExtractToDirectoryAsync(
                archivePath,
                SCRATCH_FILES_PATH,
                new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
            )
        );

        Assert.Contains("multiple entries", exception.Message);
        Assert.False(File.Exists(GetScratchPath("duplicate.txt")));
    }

    [Fact]
    public async ValueTask RequireParallel_HonorsCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ArchiveFactory.ExtractToDirectoryAsync(
                Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"),
                SCRATCH_FILES_PATH,
                new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel },
                cancellationToken: cancellationTokenSource.Token
            )
        );
    }

    [Fact]
    public async ValueTask RequireParallel_EntryFailureIncludesEntryName()
    {
        var archivePath = GetScratch2Path("overwrite-failure.zip");
        CreateZip(archivePath, ("blocked.txt", "archive"));
        File.WriteAllText(GetScratchPath("blocked.txt"), "existing");

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ArchiveFactory.ExtractToDirectoryAsync(
                archivePath,
                SCRATCH_FILES_PATH,
                new ExtractionOptions
                {
                    Overwrite = false,
                    Parallelism = ExtractionParallelism.RequireParallel,
                }
            )
        );

        Assert.Contains("blocked.txt", exception.Message);
    }

    private static void CreateZip(string archivePath, params (string Key, string Content)[] entries)
    {
        using var stream = File.Create(archivePath);
        using var writer = new ZipWriter(stream, new ZipWriterOptions(CompressionType.Deflate));

        foreach (var entry in entries)
        {
            using var entryStream = new MemoryStream(Encoding.UTF8.GetBytes(entry.Content));
            writer.Write(entry.Key, entryStream);
        }
    }
}
