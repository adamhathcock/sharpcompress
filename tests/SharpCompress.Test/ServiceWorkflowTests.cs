using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Services;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class ServiceWorkflowTests : TestBase
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FilesystemWorkflows_RoundTripThroughServicesAndExtensions(
        bool useAsync,
        bool useExtensions
    )
    {
        var composition = new ServiceComposition(
            new FactoryCatalog(() => [new ZipFactory()], () => TarWrapper.Wrappers)
        );
        var sourceDirectory = CreateScratchDirectory("source") + Path.DirectorySeparatorChar;
        var source = new FileInfo(Path.Combine(sourceDirectory, "payload.txt"));
        File.WriteAllText(source.FullName, "workflow payload");
        var destination = new FileInfo(GetScratchPath("workflow.zip"));
        var options = new ZipWriterOptions(CompressionType.Deflate);
        if (useAsync)
        {
            await using var archive = await ZipArchive.CreateAsyncArchive();
            if (useExtensions)
            {
                await archive.AddAllFromDirectoryAsync(sourceDirectory);
                await archive.AddEntryAsync("extra.txt", source);
                await archive.SaveToAsync(destination, options, CancellationToken.None);
            }
            else
            {
                await composition.FileWriter.AddAllFromDirectoryAsync(
                    archive,
                    sourceDirectory,
                    "*.*",
                    SearchOption.AllDirectories,
                    CancellationToken.None
                );
                await composition.FileWriter.AddEntryAsync(
                    archive,
                    "extra.txt",
                    source,
                    CancellationToken.None
                );
                await composition.FileWriter.SaveToAsync(
                    archive,
                    destination,
                    options,
                    CancellationToken.None
                );
            }
        }
        else
        {
            using var archive = ZipArchive.CreateArchive();
            if (useExtensions)
            {
                archive.AddAllFromDirectory(sourceDirectory);
                archive.AddEntry("extra.txt", source);
                archive.SaveTo(destination, options);
            }
            else
            {
                composition.FileWriter.AddAllFromDirectory(
                    archive,
                    sourceDirectory,
                    "*.*",
                    SearchOption.AllDirectories
                );
                composition.FileWriter.AddEntry(archive, "extra.txt", source);
                composition.FileWriter.SaveTo(archive, destination, options);
            }
        }

        var readerDirectory = CreateScratch2Directory("reader");
        var archiveDirectory = CreateScratch2Directory("archive");
        if (useAsync)
        {
            await using var reader = await composition.Readers.OpenAsyncReader(
                destination,
                cancellationToken: CancellationToken.None
            );
            await using var archive = await composition.Archives.OpenAsyncArchive(
                destination,
                cancellationToken: CancellationToken.None
            );
            if (useExtensions)
            {
                await reader.WriteAllToDirectoryAsync(
                    readerDirectory,
                    cancellationToken: CancellationToken.None
                );
                await archive.WriteToDirectoryAsync(
                    archiveDirectory,
                    cancellationToken: CancellationToken.None
                );
            }
            else
            {
                await composition.Extractor.WriteAllToDirectoryAsync(
                    reader,
                    readerDirectory,
                    cancellationToken: CancellationToken.None
                );
                await composition.Extractor.WriteToDirectoryAsync(
                    archive,
                    archiveDirectory,
                    cancellationToken: CancellationToken.None
                );
            }
        }
        else
        {
            using var reader = composition.Readers.OpenReader(destination);
            using var archive = composition.Archives.OpenArchive(destination);
            if (useExtensions)
            {
                reader.WriteAllToDirectory(readerDirectory);
                archive.WriteToDirectory(archiveDirectory);
            }
            else
            {
                composition.Extractor.WriteAllToDirectory(reader, readerDirectory);
                composition.Extractor.WriteToDirectory(archive, archiveDirectory);
            }
        }

        foreach (var directory in new[] { readerDirectory, archiveDirectory })
        {
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            Assert.Equal(
                new[] { "extra.txt", "payload.txt" },
                files.Select(Path.GetFileName).OrderBy(name => name)
            );
            Assert.All(files, file => Assert.Equal("workflow payload", File.ReadAllText(file)));
        }
        // Saving and extracting must not leave either the output or added source files open.
        using var sourceLease = source.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archiveLease = destination.Open(
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None
        );
    }
}
