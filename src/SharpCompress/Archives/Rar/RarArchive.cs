using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Extraction;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Archives.Rar;

public interface IRarArchiveCommon
{
    int MinVersion { get; }
    int MaxVersion { get; }
}

public interface IRarArchive : IArchive, IRarArchiveCommon { }

public interface IRarAsyncArchive : IAsyncArchive, IRarArchiveCommon { }

public partial class RarArchive
    : AbstractArchive<RarArchiveEntry, RarVolume>,
        IRarArchive,
        IRarAsyncArchive,
        IArchiveExtractionConcurrencyProvider
{
    private bool _disposed;
    internal Lazy<IRarUnpack> UnpackV2017 { get; } =
        new(() => new Compressors.Rar.UnpackV2017.Unpack());
    internal Lazy<IRarUnpack> UnpackV1 { get; } = new(() => new Compressors.Rar.UnpackV1.Unpack());

    private RarArchive(SourceStream sourceStream)
        : base(ArchiveType.Rar, sourceStream) { }

    async ValueTask<ArchiveExtractionConcurrencyInfo> IArchiveExtractionConcurrencyProvider.GetExtractionConcurrencyInfoAsync(
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceFiles = SourceFiles;
        var baseInformation = new ArchiveExtractionConcurrencyInfo(ArchiveType.Rar)
        {
            RequiresSeekableStream = true,
            ReaderOptions = ReaderOptions,
        };

        if (sourceFiles.Count != 1 || await IsSolidAsync().ConfigureAwait(false))
        {
            return baseInformation;
        }

        var entries = new List<RarArchiveEntry>();
        await foreach (
            var entry in EntriesAsync.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            entries.Add(entry);
        }

        var fileEntries = entries.Where(entry => !entry.IsDirectory).ToList();
        if (
            fileEntries.Count <= 1
            || fileEntries.Any(entry =>
                entry.IsEncrypted || entry.IsSplitAfter || !entry.IsComplete
            )
        )
        {
            return baseInformation;
        }

        return baseInformation with
        {
            Mode = ArchiveConcurrencyMode.IndependentEntries,
            SupportsIndependentEntryStreams = true,
            SupportsIndependentSolidStreams = true,
            SourceFile = sourceFiles[0],
            Groups = fileEntries
                .Select(entry => new ArchiveExtractionGroup(
                    new[] { entry.Key.NotNull("Entry Key is null") },
                    isSolid: false
                ))
                .ToArray(),
        };
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            if (UnpackV1.IsValueCreated && UnpackV1.Value is IDisposable unpackV1)
            {
                unpackV1.Dispose();
            }
            if (UnpackV2017.IsValueCreated && UnpackV2017.Value is IDisposable unpackV2017)
            {
                unpackV2017.Dispose();
            }

            _disposed = true;
            base.Dispose();
        }
    }

    protected override IEnumerable<RarArchiveEntry> LoadEntries(IEnumerable<RarVolume> volumes) =>
        RarArchiveEntryFactory.GetEntries(this, volumes, ReaderOptions);

    // Simple async property - kept in original file
    protected override IAsyncEnumerable<RarArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<RarVolume> volumes
    ) => RarArchiveEntryFactory.GetEntriesAsync(this, volumes, ReaderOptions);

    protected override IEnumerable<RarVolume> LoadVolumes(SourceStream sourceStream)
    {
        sourceStream.LoadAllParts();
        var streams = sourceStream.Streams.ToArray();
        var i = 0;
        if (streams.Length > 1 && IsRarFile(streams[1], ReaderOptions))
        {
            sourceStream.IsVolumes = true;
            streams[1].Position = 0;
            sourceStream.Position = 0;

            return sourceStream.Streams.Select(a => new StreamRarArchiveVolume(
                a,
                ReaderOptions,
                i++
            ));
        }

        return new StreamRarArchiveVolume(sourceStream, ReaderOptions, i++).AsEnumerable();
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        if (this.IsMultipartVolume())
        {
            var streams = Volumes.Select(volume =>
            {
                volume.Stream.Position = 0;
                return volume.Stream;
            });
            return (RarReader)RarReader.OpenReader(streams, ReaderOptions);
        }

        var stream = Volumes.First().Stream;
        stream.Position = 0;
        return (RarReader)RarReader.OpenReader(stream, ReaderOptions);
    }

    public override bool IsSolid => Volumes.First().IsSolidArchive;

    public override bool IsEncrypted => Entries.First(x => !x.IsDirectory).IsEncrypted;

    public virtual int MinVersion => Volumes.First().MinVersion;

    public virtual int MaxVersion => Volumes.First().MaxVersion;
}
