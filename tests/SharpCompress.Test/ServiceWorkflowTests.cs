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
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class ServiceWorkflowTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriterFilesystemOperations_LeaveBorrowedOutputOpen(bool useAsync)
    {
        var composition = new ServiceComposition(
            new FactoryCatalog(() => [new ZipFactory()], () => TarWrapper.Wrappers)
        );
        var directory = CreateScratchDirectory("writer-source") + Path.DirectorySeparatorChar;
        File.WriteAllText(Path.Combine(directory, "payload.txt"), "writer payload");
        using var output = new MemoryStream();
        var options = new WriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true };
        if (useAsync)
        {
            await using var writer = await composition.Writers.OpenAsyncWriter(
                output,
                ArchiveType.Zip,
                options,
                CancellationToken.None
            );
            await composition.WriterFiles.WriteAllAsync(
                writer,
                directory,
                "*",
                null,
                SearchOption.TopDirectoryOnly,
                CancellationToken.None
            );
        }
        else
        {
            using var writer = composition.Writers.OpenWriter(output, ArchiveType.Zip, options);
            composition.WriterFiles.WriteAll(
                writer,
                directory,
                "*",
                null,
                SearchOption.TopDirectoryOnly
            );
        }
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var archive = composition.Archives.OpenArchive(
            output,
            ReaderOptions.ForExternalStream
        );
        var entry = Assert.Single(archive.Entries);
        using var content = entry.OpenEntryStream();
        using var text = new StreamReader(content);
        Assert.Equal("writer payload", text.ReadToEnd());
    }

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
                await composition.ArchiveFiles.AddAllFromDirectoryAsync(
                    archive,
                    sourceDirectory,
                    "*.*",
                    SearchOption.AllDirectories,
                    CancellationToken.None
                );
                await composition.ArchiveFiles.AddEntryAsync(
                    archive,
                    "extra.txt",
                    source,
                    CancellationToken.None
                );
                await composition.ArchiveFiles.SaveToAsync(
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
                composition.ArchiveFiles.AddAllFromDirectory(
                    archive,
                    sourceDirectory,
                    "*.*",
                    SearchOption.AllDirectories
                );
                composition.ArchiveFiles.AddEntry(archive, "extra.txt", source);
                composition.ArchiveFiles.SaveTo(archive, destination, options);
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
                await composition.ReaderExtractor.WriteAllToDirectoryAsync(
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
                composition.ReaderExtractor.WriteAllToDirectory(reader, readerDirectory);
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
