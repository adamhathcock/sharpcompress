using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

// Factory registration is process-wide; isolate registration and counters from parallel tests.
[CollectionDefinition("Archive inspection reuse", DisableParallelization = true)]
public class ArchiveInspectionReuseCollection;

[Collection("Archive inspection reuse")]
public class ArchiveInspectionReuseTests : TestBase
{
    private static readonly CountingFactory countingFactory = new();

    static ArchiveInspectionReuseTests() => Factory.RegisterFactory(countingFactory);

    public ArchiveInspectionReuseTests()
    {
        countingFactory.ProbeCount = 0;
        countingFactory.OpenCount = 0;
        countingFactory.FailWithEncryptedHeaders = false;
    }

    [Theory]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    [InlineData("path", false)]
    [InlineData("path", true)]
    [InlineData("singlePath", false)]
    [InlineData("singlePath", true)]
    [InlineData("files", false)]
    [InlineData("files", true)]
    [InlineData("streams", false)]
    [InlineData("streams", true)]
    public async ValueTask InspectArchive_ReusesSelectedFactory(string source, bool useAsync)
    {
        var bytes = new byte[1024];
        bytes[0] = 0x53;
        bytes[1] = 0x43;
        bytes[2] = 0x49;
        bytes[3] = 0x54;
        using var first = new MemoryStream(bytes);
        using var second = new MemoryStream(bytes);
        var options = new ReaderOptions { LeaveStreamOpen = false };
        ArchiveInformation? information;
        if (source == "stream")
        {
            information = useAsync
                ? await ArchiveFactory.InspectArchiveAsync(first, options)
                : ArchiveFactory.InspectArchive(first, options);
        }
        else if (source == "streams")
        {
            Stream[] streams = [first, second];
            information = useAsync
                ? await ArchiveFactory.InspectArchiveAsync(streams, options)
                : ArchiveFactory.InspectArchive(streams, options);
        }
        else
        {
            var path = GetScratchPath("inspection.reuse");
            File.WriteAllBytes(path, bytes);
            if (source != "singlePath")
            {
                File.WriteAllBytes(path + ".part2", bytes);
            }
            FileInfo[] files = [new(path), new(path + ".part2")];
            information = source is "path" or "singlePath"
                ? useAsync
                    ? await ArchiveFactory.InspectArchiveAsync(path, options)
                    : ArchiveFactory.InspectArchive(path, options)
                : useAsync
                    ? await ArchiveFactory.InspectArchiveAsync(files, options)
                    : ArchiveFactory.InspectArchive(files, options);
        }

        Assert.NotNull(information);
        Assert.Equal("Inspection test", information.Detection.FormatName);
        Assert.Equal(ArchiveInformationStatus.Complete, information.Status);
        Assert.Equal(source is "stream" or "singlePath" ? 1 : 2, information.PhysicalPartCount);
        Assert.Equal(1, countingFactory.ProbeCount);
        Assert.Equal(1, countingFactory.OpenCount);
        Assert.True(first.CanRead);
        Assert.True(second.CanRead);
        Assert.Equal(0, first.Position);
        Assert.Equal(0, second.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async ValueTask InspectArchive_PartialInformation_ReusesDetection(bool useAsync)
    {
        countingFactory.FailWithEncryptedHeaders = true;
        using var stream = new MemoryStream(new byte[1024]);
        stream.Write(new byte[] { 0x53, 0x43, 0x49, 0x54 }, 0, 4);
        stream.Position = 0;

        var information = useAsync
            ? await ArchiveFactory.InspectArchiveAsync(stream)
            : ArchiveFactory.InspectArchive(stream);

        Assert.NotNull(information);
        Assert.Equal(ArchiveInformationLimitations.EncryptedHeaders, information.Limitations);
        Assert.Equal(1, countingFactory.ProbeCount);
        Assert.Equal(1, countingFactory.OpenCount);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    private sealed class CountingFactory : Factory, IArchiveFactory, IMultiArchiveFactory
    {
        public int ProbeCount;
        public int OpenCount;
        public bool FailWithEncryptedHeaders;

        public override string Name => "Inspection test";

        public override IEnumerable<string> GetSupportedExtensions() => ["reuse"];

        public override bool IsArchive(Stream stream, ReaderOptions readerOptions)
        {
            if (
                stream.ReadByte() != 0x53
                || stream.ReadByte() != 0x43
                || stream.ReadByte() != 0x49
                || stream.ReadByte() != 0x54
            )
            {
                return false;
            }
            ProbeCount++;
            return true;
        }

        public override ValueTask<bool> IsArchiveAsync(
            Stream stream,
            ReaderOptions readerOptions,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(IsArchive(stream, readerOptions));
        }

        public override FileInfo? GetFilePart(int index, FileInfo part1) =>
            index == 1 && File.Exists(part1.FullName + ".part2")
                ? new FileInfo(part1.FullName + ".part2")
                : null;

        private MemoryStream OpenSource()
        {
            OpenCount++;
            if (FailWithEncryptedHeaders)
            {
                throw new CryptographicException("Test encrypted headers");
            }
            // Supply a valid empty TAR after selection; the synthetic signature is only for probing.
            return new MemoryStream(new byte[1024]);
        }

        public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
            TarArchive.OpenArchive(OpenSource(), ReaderOptions.ForFilePath);

        public async ValueTask<IAsyncArchive> OpenAsyncArchive(
            Stream stream,
            ReaderOptions? readerOptions = null,
            CancellationToken cancellationToken = default
        ) =>
            await TarArchive
                .OpenAsyncArchive(OpenSource(), ReaderOptions.ForFilePath, cancellationToken)
                .ConfigureAwait(false);

        public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
            OpenArchive(Stream.Null, readerOptions);

        public ValueTask<IAsyncArchive> OpenAsyncArchive(
            FileInfo fileInfo,
            ReaderOptions? readerOptions = null,
            CancellationToken cancellationToken = default
        ) => OpenAsyncArchive(Stream.Null, readerOptions, cancellationToken);

        public IArchive OpenArchive(
            IReadOnlyList<FileInfo> fileInfos,
            ReaderOptions? readerOptions = null
        ) => OpenArchive(Stream.Null, readerOptions);

        public ValueTask<IAsyncArchive> OpenAsyncArchive(
            IReadOnlyList<FileInfo> fileInfos,
            ReaderOptions? readerOptions = null,
            CancellationToken cancellationToken = default
        ) => OpenAsyncArchive(Stream.Null, readerOptions, cancellationToken);

        public IArchive OpenArchive(
            IReadOnlyList<Stream> streams,
            ReaderOptions? readerOptions = null
        ) => OpenArchive(Stream.Null, readerOptions);

        public ValueTask<IAsyncArchive> OpenAsyncArchive(
            IReadOnlyList<Stream> streams,
            ReaderOptions? readerOptions = null,
            CancellationToken cancellationToken = default
        ) => OpenAsyncArchive(Stream.Null, readerOptions, cancellationToken);
    }
}
