using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Readers;
using Xunit;

#pragma warning disable CS0618 // Intentionally exercise obsolete ArchiveFactory compatibility entry points.

namespace SharpCompress.Test;

public class ArchiveFacadeTests : TestBase
{
    [Fact]
    public void ArchiveFactory_OnlyForwardingMethods_AreObsoleteWarnings()
    {
        var methods = typeof(ArchiveFactory).GetMethods(
            BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly
        );
        var obsoleteCount = 0;
        foreach (var method in methods)
        {
            var obsolete = method.GetCustomAttribute<ObsoleteAttribute>();
            if (
                method.Name
                is "WriteToDirectory"
                    or "FindFactory"
                    or "FindFactoryAsync"
                    or "IsArchive"
                    or "IsArchiveAsync"
            )
            {
                Assert.NotNull(obsolete);
                Assert.False(obsolete.IsError);
                var replacement =
                    method.Name == "WriteToDirectory" ? "ArchiveExtractor" : "ArchiveDetector";
                Assert.Contains($"{replacement}.{method.Name}", obsolete.Message);
                obsoleteCount++;
            }
            else
            {
                Assert.Null(obsolete);
            }
        }

        Assert.Equal(15, obsoleteCount);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", false)]
    [InlineData("Zip.deflate.zip", true)]
    [InlineData("Tar.noEmptyDirs.tar", false)]
    [InlineData("Tar.noEmptyDirs.tar", true)]
    public async Task DetectionAndInspection_LeaveBorrowedStreamOpen(
        string archiveName,
        bool useAsync
    )
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archiveName));
        var options = new ReaderOptions { LeaveStreamOpen = false };
        if (useAsync)
        {
            Assert.NotNull(
                await ArchiveDetector.DetectArchiveAsync(stream, options, CancellationToken.None)
            );
            Assert.Equal(0, stream.Position);
            Assert.NotNull(
                await ArchiveInspector.InspectArchiveAsync(stream, options, CancellationToken.None)
            );
        }
        else
        {
            Assert.NotNull(ArchiveDetector.DetectArchive(stream, options));
            Assert.Equal(0, stream.Position);
            Assert.NotNull(ArchiveInspector.InspectArchive(stream, options));
        }

        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Tar.noEmptyDirs.tar")]
    public async Task Detector_LegacyEntryPoints_ReturnSameResults(string archiveName)
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, archiveName);
        var file = new FileInfo(path);
        using var stream = File.OpenRead(path);
        Assert.Same(
            ArchiveFactory.FindFactory<IArchiveFactory>(path),
            ArchiveDetector.FindFactory<IArchiveFactory>(path)
        );
        Assert.Same(
            ArchiveFactory.FindFactory<IArchiveFactory>(file),
            ArchiveDetector.FindFactory<IArchiveFactory>(file)
        );
        Assert.Same(
            ArchiveFactory.FindFactory<IArchiveFactory>(stream),
            ArchiveDetector.FindFactory<IArchiveFactory>(stream)
        );
        var options = ReaderOptions.ForExternalStream;
        Assert.Equal(
            ArchiveFactory.IsArchive(path, out var legacyType),
            ArchiveDetector.IsArchive(path, out var type)
        );
        Assert.Equal(legacyType, type);
        Assert.Equal(
            ArchiveFactory.IsArchive(stream, options, out legacyType),
            ArchiveDetector.IsArchive(stream, options, out type)
        );
        Assert.Equal(legacyType, type);
        Assert.Equal(
            await ArchiveFactory.IsArchiveAsync(path, CancellationToken.None),
            await ArchiveDetector.IsArchiveAsync(path, CancellationToken.None)
        );
        Assert.Equal(
            await ArchiveFactory.IsArchiveAsync(stream, options, CancellationToken.None),
            await ArchiveDetector.IsArchiveAsync(stream, options, CancellationToken.None)
        );
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Extractor_LegacyAndFocusedEntryPoints_ExtractArchive(bool useLegacy)
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");
        var destination = CreateScratchDirectory("extraction");
        if (useLegacy)
        {
            ArchiveFactory.WriteToDirectory(
                path,
                destination,
                new ExtractionOptions { ExtractFullPath = true }
            );
        }
        else
        {
            ArchiveExtractor.WriteToDirectory(
                path,
                destination,
                new ExtractionOptions { ExtractFullPath = true }
            );
        }

        using var archive = ArchiveFactory.OpenArchive(path);
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            var extracted = Path.Combine(destination, entry.Key!);
            Assert.True(File.Exists(extracted));
            using var expected = entry.OpenEntryStream();
            using var contents = new MemoryStream();
            expected.CopyTo(contents);
            Assert.Equal(contents.ToArray(), File.ReadAllBytes(extracted));
        }
    }
}
