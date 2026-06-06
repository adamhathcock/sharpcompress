using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarArchiveAsyncFactoryExtractionTests : ArchiveTests
{
    [Fact]
    public async ValueTask ExtractToDirectoryAsync_NonSolid_RequireParallel_Extracts()
    {
        await ArchiveFactory.ExtractToDirectoryAsync(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar"),
            SCRATCH_FILES_PATH,
            new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
        );

        VerifyFiles();
    }

    [Fact]
    public async ValueTask ExtractToDirectoryAsync_Solid_RequireParallel_Fails()
    {
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await ArchiveFactory.ExtractToDirectoryAsync(
                Path.Combine(TEST_ARCHIVES_PATH, "Rar.solid.rar"),
                SCRATCH_FILES_PATH,
                new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
            )
        );
    }
}
