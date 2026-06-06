using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipArchiveAsyncFactoryExtractionTests : ArchiveTests
{
    [Fact]
    public async ValueTask ExtractToDirectoryAsync_NonSolid_RequireParallel_Extracts()
    {
        await ArchiveFactory.ExtractToDirectoryAsync(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.nonsolid.7z"),
            SCRATCH_FILES_PATH,
            new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
        );

        VerifyFiles();
    }

    [Fact]
    public async ValueTask ExtractToDirectoryAsync_SolidSingleBlock_RequireParallel_Fails()
    {
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await ArchiveFactory.ExtractToDirectoryAsync(
                Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.1block.7z"),
                SCRATCH_FILES_PATH,
                new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
            )
        );
    }
}
