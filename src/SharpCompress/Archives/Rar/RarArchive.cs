using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public partial class RarArchive : AbstractArchive<RarArchiveEntry, RarVolume>, IRarArchive
{
    private bool _disposed;
    internal Lazy<IRarUnpack> UnpackV2017 { get; } =
        new(() => new Compressors.Rar.UnpackV2017.Unpack());
    internal Lazy<IRarUnpack> UnpackV1 { get; } = new(() => new Compressors.Rar.UnpackV1.Unpack());

    private RarArchive(SourceStream sourceStream)
        : base(ArchiveType.Rar, sourceStream) { }

    public override void Dispose()
    {
        if (!_disposed)
        {
            if (UnpackV1.IsValueCreated && UnpackV1.Value is IDisposable unpackV1)
            {
                unpackV1.Dispose();
            }

            _disposed = true;
            base.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (UnpackV1.IsValueCreated && UnpackV1.Value is IDisposable unpackV1)
            {
                unpackV1.Dispose();
            }

            _disposed = true;
            await base.DisposeAsync();
        }
    }

    protected override IEnumerable<RarArchiveEntry> LoadEntries(IEnumerable<RarVolume> volumes) =>
        RarArchiveEntryFactory.GetEntries(this, volumes, ReaderOptions);

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

    protected override IReader CreateReaderForSolidExtraction() =>
        CreateReaderForSolidExtractionInternal();

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync() =>
        new(CreateReaderForSolidExtractionInternal());

    private RarReader CreateReaderForSolidExtractionInternal()
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

    public override async ValueTask<bool> IsSolidAsync() =>
        await (await VolumesAsync.CastAsync<RarVolume>().FirstAsync()).IsSolidArchiveAsync();

    public override bool IsEncrypted => Entries.First(x => !x.IsDirectory).IsEncrypted;

    public virtual int MinVersion => Volumes.First().MinVersion;

    public virtual int MaxVersion => Volumes.First().MaxVersion;
}
