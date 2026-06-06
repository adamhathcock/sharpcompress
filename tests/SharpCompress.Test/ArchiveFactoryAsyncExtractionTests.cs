using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test;

public class ArchiveFactoryAsyncExtractionTests : ArchiveTests
{
    public ArchiveFactoryAsyncExtractionTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask ExtractToDirectoryAsync_Zip_DefaultAuto_Extracts()
    {
        await ArchiveFactory.ExtractToDirectoryAsync(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"),
            SCRATCH_FILES_PATH
        );

        VerifyFiles();
    }

    [Fact]
    public async ValueTask ExtractToDirectoryAsync_Zip_FileInfo_RequireParallel_Extracts()
    {
        await ArchiveFactory.ExtractToDirectoryAsync(
            new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")),
            SCRATCH_FILES_PATH,
            new ExtractionOptions { Parallelism = ExtractionParallelism.RequireParallel }
        );

        VerifyFiles();
    }

    [Fact]
    public async ValueTask ExtractToDirectoryAsync_Zip_SingleThreaded_Extracts()
    {
        await ArchiveFactory.ExtractToDirectoryAsync(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"),
            SCRATCH_FILES_PATH,
            new ExtractionOptions { Parallelism = ExtractionParallelism.SingleThreaded }
        );

        VerifyFiles();
    }

    [Fact]
    public async ValueTask ExtractToDirectoryAsync_ForwardsProgress()
    {
        var progressReports = new List<ProgressReport>();
        var progress = new SynchronousProgress<ProgressReport>(progressReports.Add);

        await ArchiveFactory.ExtractToDirectoryAsync(
            Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"),
            SCRATCH_FILES_PATH,
            progress: progress
        );

        VerifyFiles();
        Assert.NotEmpty(progressReports);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> handler;

        internal SynchronousProgress(Action<T> handler) => this.handler = handler;

        public void Report(T value) => handler(value);
    }
}
