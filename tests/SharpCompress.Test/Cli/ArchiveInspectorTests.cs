#if NET10_0_OR_GREATER
using System.IO;
using SharpCompress.Cli;
using SharpCompress.Cli.Inspection;
using Xunit;

namespace SharpCompress.Test.Cli;

public class ArchiveInspectorTests : TestBase
{
    [Fact]
    public void Forward_Default_UsesForward_ForSimpleZipArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");

        var result = inspector.InspectArchives([archivePath], new InspectionRequest());

        Assert.Empty(result.Errors);
        var archive = Assert.Single(result.Archives);
        Assert.Equal(AccessMode.Forward, archive.RequestedAccessMode);
        Assert.Equal(AccessMode.Forward, archive.UsedAccessMode);
        Assert.False(archive.AutoFallbackApplied);
        Assert.Null(archive.FallbackReason);
        Assert.Equal("Zip", archive.ArchiveType);
        Assert.True(archive.EntryCount > 0);
    }

    [Fact]
    public void Seekable_Mode_ForcesSeekable_ForSimpleZipArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");

        var result = inspector.InspectArchives(
            [archivePath],
            new InspectionRequest { AccessMode = AccessMode.Seekable }
        );

        Assert.Empty(result.Errors);
        var archive = Assert.Single(result.Archives);
        Assert.Equal(AccessMode.Seekable, archive.RequestedAccessMode);
        Assert.Equal(AccessMode.Seekable, archive.UsedAccessMode);
        Assert.False(archive.AutoFallbackApplied);
        Assert.True(archive.IsComplete);
    }

    [Fact]
    public void Forward_Default_FallsBackToSeekable_ForSplitZipArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.split.001");

        var result = inspector.InspectArchives([archivePath], new InspectionRequest());

        Assert.Empty(result.Errors);
        var archive = Assert.Single(result.Archives);
        Assert.Equal(AccessMode.Forward, archive.RequestedAccessMode);
        Assert.Equal(AccessMode.Seekable, archive.UsedAccessMode);
        Assert.True(archive.AutoFallbackApplied);
        Assert.Contains("multi-volume", archive.FallbackReason);
        Assert.True(archive.VolumeCount > 1);
    }

    [Fact]
    public void Seekable_Mode_ShowsActionableError_ForForwardOnlyArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Ace.method1.ace");

        var result = inspector.InspectArchives(
            [archivePath],
            new InspectionRequest { AccessMode = AccessMode.Seekable }
        );

        Assert.Empty(result.Archives);
        var error = Assert.Single(result.Errors);
        Assert.Equal(archivePath, error.ArchivePath);
        Assert.Contains("--access forward", error.Message);
    }
}
#endif
