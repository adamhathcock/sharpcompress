using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Common;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipArchiveAsyncFactoryExtractionTests : TestBase
{
    [Fact]
    public async ValueTask ExtractToDirectoryAsync_RequireParallel_Fails()
    {
        var archivePath = GetScratch2Path("single.gz");
        var entryBytes = Encoding.UTF8.GetBytes("single file content");

        await using (var archive = (GZipArchive)await GZipArchive.CreateAsyncArchive())
        {
            await archive.AddEntryAsync(
                "single.txt",
                new MemoryStream(entryBytes),
                closeStream: true,
                size: entryBytes.Length
            );
            await archive.SaveToAsync(archivePath, new GZipWriterOptions());
        }

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await ArchiveFactory.ExtractToDirectoryAsync(
                archivePath,
                SCRATCH_FILES_PATH,
                new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
            )
        );
    }
}
