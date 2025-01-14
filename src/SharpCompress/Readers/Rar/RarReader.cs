using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Compressors.Rar;

namespace SharpCompress.Readers.Rar;

/// <summary>
/// This class faciliates Reading a Rar Archive in a non-seekable forward-only manner
/// </summary>
public abstract class RarReader : AbstractReader<RarReaderEntry, RarVolume>
{
    private bool _disposed;
    private RarVolume? volume;
    private Lazy<IRarUnpack> UnpackV2017 { get; } =
        new(() => new Compressors.Rar.UnpackV2017.Unpack());
    private Lazy<IRarUnpack> UnpackV1 { get; } = new(() => new Compressors.Rar.UnpackV1.Unpack());

    internal RarReader(ReaderOptions options)
        : base(options, ArchiveType.Rar) { }

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

    protected abstract void ValidateArchive(RarVolume archive);

    public override RarVolume? Volume => volume;

    /// <summary>
    /// Opens a RarReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static RarReader Open(Stream stream, ReaderOptions? options = null)
    {
        stream.CheckNotNull(nameof(stream));
        return new SingleVolumeRarReader(stream, options ?? new ReaderOptions());
    }

    /// <summary>
    /// Opens a RarReader for Non-seeking usage with multiple volumes
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static RarReader Open(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.CheckNotNull(nameof(streams));
        return new MultiVolumeRarReader(streams, options ?? new ReaderOptions());
    }

    protected override IEnumerable<RarReaderEntry> GetEntries(Stream stream)
    {
        volume = new RarReaderVolume(stream, Options, 0);
        foreach (var fp in volume.ReadFileParts())
        {
            ValidateArchive(volume);
            yield return new RarReaderEntry(volume.IsSolidArchive, fp);
        }
    }

    protected virtual IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry() =>
        Entry.Parts;

    protected override EntryStream GetEntryStream()
    {
        if (Entry.IsRedir)
        {
            throw new InvalidOperationException("no stream for redirect entry");
        }

        var stream = new MultiVolumeReadOnlyStream(
            CreateFilePartEnumerableForCurrentEntry().Cast<RarFilePart>(),
            this
        );
        if (Entry.IsRarV3)
        {
            return CreateEntryStream(new RarCrcStream(UnpackV1.Value, Entry.FileHeader, stream));
        }

        if (Entry.FileHeader.FileCrc.Length > 5)
        {
            return CreateEntryStream(
                new RarBLAKE2spStream(UnpackV2017.Value, Entry.FileHeader, stream)
            );
        }

        return CreateEntryStream(new RarCrcStream(UnpackV2017.Value, Entry.FileHeader, stream));
    }
}
