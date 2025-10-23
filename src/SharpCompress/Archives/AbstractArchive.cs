using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public abstract class AbstractArchive<TEntry, TVolume> : IArchive, IArchiveExtractionListener
    where TEntry : IArchiveEntry
    where TVolume : IVolume
{
    private readonly LazyReadOnlyCollection<TVolume> _lazyVolumes;
    private readonly LazyReadOnlyCollection<TEntry> _lazyEntries;
    private bool _disposed;
    private readonly SourceStream? _sourceStream;

    public event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>>? EntryExtractionBegin;
    public event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>>? EntryExtractionEnd;

    public event EventHandler<CompressedBytesReadEventArgs>? CompressedBytesRead;
    public event EventHandler<FilePartExtractionBeginEventArgs>? FilePartExtractionBegin;
    protected ReaderOptions ReaderOptions { get; }

    internal AbstractArchive(ArchiveType type, SourceStream sourceStream)
    {
        Type = type;
        ReaderOptions = sourceStream.ReaderOptions;
        _sourceStream = sourceStream;
        _lazyVolumes = new LazyReadOnlyCollection<TVolume>(LoadVolumes(_sourceStream));
        _lazyEntries = new LazyReadOnlyCollection<TEntry>(LoadEntries(Volumes));
    }

    internal AbstractArchive(ArchiveType type)
    {
        Type = type;
        ReaderOptions = new();
        _lazyVolumes = new LazyReadOnlyCollection<TVolume>(Enumerable.Empty<TVolume>());
        _lazyEntries = new LazyReadOnlyCollection<TEntry>(Enumerable.Empty<TEntry>());
    }

    public ArchiveType Type { get; }

    void IArchiveExtractionListener.FireEntryExtractionBegin(IArchiveEntry entry) =>
        EntryExtractionBegin?.Invoke(this, new ArchiveExtractionEventArgs<IArchiveEntry>(entry));

    void IArchiveExtractionListener.FireEntryExtractionEnd(IArchiveEntry entry) =>
        EntryExtractionEnd?.Invoke(this, new ArchiveExtractionEventArgs<IArchiveEntry>(entry));

    private static Stream CheckStreams(Stream stream)
    {
        if (!stream.CanSeek || !stream.CanRead)
        {
            throw new ArchiveException("Archive streams must be Readable and Seekable");
        }
        return stream;
    }

    /// <summary>
    /// Returns an ReadOnlyCollection of all the RarArchiveEntries across the one or many parts of the RarArchive.
    /// </summary>
    public virtual ICollection<TEntry> Entries => _lazyEntries;

    /// <summary>
    /// Returns an ReadOnlyCollection of all the RarArchiveVolumes across the one or many parts of the RarArchive.
    /// </summary>
    public ICollection<TVolume> Volumes => _lazyVolumes;

    /// <summary>
    /// The total size of the files compressed in the archive.
    /// </summary>
    public virtual long TotalSize =>
        Entries.Aggregate(0L, (total, cf) => total + cf.CompressedSize);

    /// <summary>
    /// The total size of the files as uncompressed in the archive.
    /// </summary>
    public virtual long TotalUncompressSize =>
        Entries.Aggregate(0L, (total, cf) => total + cf.Size);

    protected abstract IEnumerable<TVolume> LoadVolumes(SourceStream sourceStream);
    protected abstract IEnumerable<TEntry> LoadEntries(IEnumerable<TVolume> volumes);

    IEnumerable<IArchiveEntry> IArchive.Entries => Entries.Cast<IArchiveEntry>();

    IEnumerable<IVolume> IArchive.Volumes => _lazyVolumes.Cast<IVolume>();

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _lazyVolumes.ForEach(v => v.Dispose());
            _lazyEntries.GetLoaded().Cast<Entry>().ForEach(x => x.Close());
            _sourceStream?.Dispose();

            _disposed = true;
        }
    }

    void IArchiveExtractionListener.EnsureEntriesLoaded()
    {
        _lazyEntries.EnsureFullyLoaded();
        _lazyVolumes.EnsureFullyLoaded();
    }

    void IExtractionListener.FireCompressedBytesRead(
        long currentPartCompressedBytes,
        long compressedReadBytes
    ) =>
        CompressedBytesRead?.Invoke(
            this,
            new CompressedBytesReadEventArgs(
                currentFilePartCompressedBytesRead: currentPartCompressedBytes,
                compressedBytesRead: compressedReadBytes
            )
        );

    void IExtractionListener.FireFilePartExtractionBegin(
        string name,
        long size,
        long compressedSize
    ) =>
        FilePartExtractionBegin?.Invoke(
            this,
            new FilePartExtractionBeginEventArgs(
                compressedSize: compressedSize,
                size: size,
                name: name
            )
        );

    /// <summary>
    /// Use this method to extract all entries in an archive in order.
    /// This is primarily for SOLID Rar Archives or 7Zip Archives as they need to be
    /// extracted sequentially for the best performance.
    ///
    /// This method will load all entry information from the archive.
    ///
    /// WARNING: this will reuse the underlying stream for the archive.  Errors may
    /// occur if this is used at the same time as other extraction methods on this instance.
    /// </summary>
    /// <returns></returns>
    public IReader ExtractAllEntries()
    {
        if (!IsSolid && Type != ArchiveType.SevenZip)
        {
            throw new InvalidOperationException(
                "ExtractAllEntries can only be used on solid archives or 7Zip archives (which require random access)."
            );
        }
        ((IArchiveExtractionListener)this).EnsureEntriesLoaded();
        return CreateReaderForSolidExtraction();
    }

    protected abstract IReader CreateReaderForSolidExtraction();

    /// <summary>
    /// Archive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
    /// </summary>
    public virtual bool IsSolid => false;

    /// <summary>
    /// The archive can find all the parts of the archive needed to fully extract the archive.  This forces the parsing of the entire archive.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            ((IArchiveExtractionListener)this).EnsureEntriesLoaded();
            return Entries.All(x => x.IsComplete);
        }
    }
}
