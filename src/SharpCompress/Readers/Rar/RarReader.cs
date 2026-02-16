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
public abstract partial class RarReader : AbstractReader<RarReaderEntry, RarVolume>
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

    public static IReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        readerOptions ??= new ReaderOptions { LeaveStreamOpen = false };
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }

    public static IReader OpenReader(IEnumerable<string> filePaths, ReaderOptions? options = null)
    {
        return OpenReader(filePaths.Select(x => new FileInfo(x)), options);
    }

    public static IReader OpenReader(IEnumerable<FileInfo> fileInfos, ReaderOptions? options = null)
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };
        return OpenReader(fileInfos.Select(x => x.OpenRead()), options);
    }

    /// <summary>
    /// Opens a RarReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new SingleVolumeRarReader(stream, readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Opens a RarReader for Non-seeking usage with multiple volumes
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.NotNull(nameof(streams));
        return new MultiVolumeRarReader(streams, options ?? new ReaderOptions());
    }

    protected override IEnumerable<RarReaderEntry> GetEntries(Stream stream)
    {
        volume = new RarReaderVolume(stream, Options, 0);
        foreach (var fp in volume.ReadFileParts())
        {
            ValidateArchive(volume);
            yield return new RarReaderEntry(volume.IsSolidArchive, fp, Options);
        }
    }

    protected override async IAsyncEnumerable<RarReaderEntry> GetEntriesAsync(Stream stream)
    {
        volume = new RarReaderVolume(stream, Options, 0);
        await foreach (var fp in volume.ReadFilePartsAsync())
        {
            ValidateArchive(volume);
            yield return new RarReaderEntry(volume.IsSolidArchive, fp, Options);
        }
    }

    protected virtual IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry() =>
        Entry.Parts;

    protected override EntryStream GetEntryStream()
    {
        if (Entry.IsRedir)
        {
            throw new ArchiveOperationException("no stream for redirect entry");
        }

        var stream = new MultiVolumeReadOnlyStream(
            CreateFilePartEnumerableForCurrentEntry().Cast<RarFilePart>()
        );
        if (Entry.IsRarV3)
        {
            return CreateEntryStream(RarCrcStream.Create(UnpackV1.Value, Entry.FileHeader, stream));
        }

        if (Entry.FileHeader.FileCrc?.Length > 5)
        {
            return CreateEntryStream(
                RarBLAKE2spStream.Create(UnpackV2017.Value, Entry.FileHeader, stream)
            );
        }

        return CreateEntryStream(RarCrcStream.Create(UnpackV2017.Value, Entry.FileHeader, stream));
    }

    // GetEntryStreamAsync moved to RarReader.Async.cs
}
