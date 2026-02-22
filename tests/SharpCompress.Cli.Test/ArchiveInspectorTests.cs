using System;
using System.IO;
using SharpCompress.Cli;
using SharpCompress.Cli.Inspection;
using Xunit;

namespace SharpCompress.Cli.Test;

public class ArchiveInspectorTests
{
    private static readonly string TestArchivesPath = FindTestArchivesPath();

    [Fact]
    public void Forward_Default_UsesForward_ForSimpleZipArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TestArchivesPath, "Zip.deflate.zip");

        var result = inspector.InspectArchives([archivePath], new InspectionRequest());

        Assert.Empty(result.Errors);
        var archive = Assert.Single(result.Archives);
        Assert.Equal(AccessMode.Forward, archive.RequestedAccessMode);
        Assert.Equal(AccessMode.Forward, archive.UsedAccessMode);
        Assert.Equal(StreamingType.Forward, archive.StreamingType);
        Assert.False(archive.AutoFallbackApplied);
        Assert.Null(archive.FallbackReason);
        Assert.Equal(SharpCompress.Common.ArchiveType.Zip, archive.DetectedArchiveType);
        Assert.Equal("Zip", archive.ArchiveType);
        Assert.True(archive.EntryCount > 0);
    }

    [Fact]
    public void Seekable_Mode_ForcesSeekable_ForSimpleZipArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TestArchivesPath, "Zip.deflate.zip");

        var result = inspector.InspectArchives(
            [archivePath],
            new InspectionRequest { AccessMode = AccessMode.Seekable }
        );

        Assert.Empty(result.Errors);
        var archive = Assert.Single(result.Archives);
        Assert.Equal(AccessMode.Seekable, archive.RequestedAccessMode);
        Assert.Equal(AccessMode.Seekable, archive.UsedAccessMode);
        Assert.Equal(StreamingType.Seekable, archive.StreamingType);
        Assert.False(archive.AutoFallbackApplied);
        Assert.True(archive.IsComplete);
    }

    [Fact]
    public void Forward_Default_FallsBackToSeekable_ForSplitZipArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TestArchivesPath, "Zip.deflate.split.001");

        var result = inspector.InspectArchives([archivePath], new InspectionRequest());

        Assert.Empty(result.Errors);
        var archive = Assert.Single(result.Archives);
        Assert.Equal(AccessMode.Forward, archive.RequestedAccessMode);
        Assert.Equal(AccessMode.Seekable, archive.UsedAccessMode);
        Assert.Equal(StreamingType.SeekableMultiVolume, archive.StreamingType);
        Assert.True(archive.AutoFallbackApplied);
        Assert.Contains("multi-volume", archive.FallbackReason);
        Assert.True(archive.VolumeCount > 1);
    }

    [Fact]
    public void Seekable_Mode_ShowsActionableError_ForForwardOnlyArchive()
    {
        var inspector = new ArchiveInspector();
        var archivePath = Path.Combine(TestArchivesPath, "Ace.method1.ace");

        var result = inspector.InspectArchives(
            [archivePath],
            new InspectionRequest { AccessMode = AccessMode.Seekable }
        );

        Assert.Empty(result.Archives);
        var error = Assert.Single(result.Errors);
        Assert.Equal(archivePath, error.ArchivePath);
        Assert.Equal(InspectionErrorCode.AccessModeNotSupported, error.Code);
        Assert.Contains("--access forward", error.Message);
    }

    [Fact]
    public void DetectsNonArchiveFilesWithoutThrowing()
    {
        var inspector = new ArchiveInspector();
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"sharpcompress-cli-{Guid.NewGuid():N}.txt"
        );
        File.WriteAllText(filePath, "not an archive");

        try
        {
            var result = inspector.InspectArchives([filePath], new InspectionRequest());

            Assert.Empty(result.Archives);
            var error = Assert.Single(result.Errors);
            Assert.Equal(filePath, error.ArchivePath);
            Assert.Equal(InspectionErrorCode.NotArchive, error.Code);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string FindTestArchivesPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "TestArchives", "Archives");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate tests/TestArchives/Archives.");
    }
}
