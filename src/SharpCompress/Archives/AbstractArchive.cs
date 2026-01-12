using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public abstract class AbstractArchive<TEntry, TVolume> : IArchive, IAsyncArchive
    where TEntry : IArchiveEntry
    where TVolume : IVolume
{
    private readonly LazyReadOnlyCollection<TVolume> _lazyVolumes;
    private readonly LazyReadOnlyCollection<TEntry> _lazyEntries;
    private bool _disposed;
    private readonly SourceStream? _sourceStream;

    protected ReaderOptions ReaderOptions { get; }

    internal AbstractArchive(ArchiveType type, SourceStream sourceStream)
    {
        Type = type;
        ReaderOptions = sourceStream.ReaderOptions;
        _sourceStream = sourceStream;
        _lazyVolumes = new LazyReadOnlyCollection<TVolume>(LoadVolumes(_sourceStream));
        _lazyEntries = new LazyReadOnlyCollection<TEntry>(LoadEntries(Volumes));
        _lazyVolumesAsync = new LazyAsyncReadOnlyCollection<TVolume>(
            LoadVolumesAsync(_sourceStream)
        );
        _lazyEntriesAsync = new LazyAsyncReadOnlyCollection<TEntry>(
            LoadEntriesAsync(_lazyVolumesAsync)
        );
    }

    internal AbstractArchive(ArchiveType type)
    {
        Type = type;
        ReaderOptions = new();
        _lazyVolumes = new LazyReadOnlyCollection<TVolume>(Enumerable.Empty<TVolume>());
        _lazyEntries = new LazyReadOnlyCollection<TEntry>(Enumerable.Empty<TEntry>());
        _lazyVolumesAsync = new LazyAsyncReadOnlyCollection<TVolume>(
            AsyncEnumerableEx.Empty<TVolume>()
        );
        _lazyEntriesAsync = new LazyAsyncReadOnlyCollection<TEntry>(
            AsyncEnumerableEx.Empty<TEntry>()
        );
    }

    public ArchiveType Type { get; }

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

    protected virtual IAsyncEnumerable<TVolume> LoadVolumesAsync(SourceStream sourceStream) =>
        LoadVolumes(sourceStream).ToAsyncEnumerable();

    protected virtual async IAsyncEnumerable<TEntry> LoadEntriesAsync(
        IAsyncEnumerable<TVolume> volumes
    )
    {
        foreach (var item in LoadEntries(await volumes.ToListAsync()))
        {
            yield return item;
        }
    }

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

    private void EnsureEntriesLoaded()
    {
        _lazyEntries.EnsureFullyLoaded();
        _lazyVolumes.EnsureFullyLoaded();
    }

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
            throw new SharpCompressException(
                "ExtractAllEntries can only be used on solid archives or 7Zip archives (which require random access)."
            );
        }
        EnsureEntriesLoaded();
        return CreateReaderForSolidExtraction();
    }

    protected abstract IReader CreateReaderForSolidExtraction();
    protected abstract ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync();

    /// <summary>
    /// Archive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
    /// </summary>
    public virtual bool IsSolid => false;

    /// <summary>
    /// Archive is ENCRYPTED (this means the Archive has password-protected files).
    /// </summary>
    public virtual bool IsEncrypted => false;

    /// <summary>
    /// The archive can find all the parts of the archive needed to fully extract the archive.  This forces the parsing of the entire archive.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            EnsureEntriesLoaded();
            return Entries.All(x => x.IsComplete);
        }
    }

    #region Async Support

    private readonly LazyAsyncReadOnlyCollection<TVolume> _lazyVolumesAsync;
    private readonly LazyAsyncReadOnlyCollection<TEntry> _lazyEntriesAsync;

    public virtual async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await foreach (var v in _lazyVolumesAsync)
            {
                v.Dispose();
            }
            foreach (var v in _lazyEntriesAsync.GetLoaded().Cast<Entry>())
            {
                v.Close();
            }
            _sourceStream?.Dispose();

            _disposed = true;
        }
    }

    private async ValueTask EnsureEntriesLoadedAsync()
    {
        await _lazyEntriesAsync.EnsureFullyLoaded();
        await _lazyVolumesAsync.EnsureFullyLoaded();
    }

    public virtual IAsyncEnumerable<TEntry> EntriesAsync => _lazyEntriesAsync;

    private async IAsyncEnumerable<IArchiveEntry> EntriesAsyncCast()
    {
        await foreach (var entry in EntriesAsync)
        {
            yield return entry;
        }
    }

    IAsyncEnumerable<IArchiveEntry> IAsyncArchive.EntriesAsync => EntriesAsyncCast();

    private async IAsyncEnumerable<IVolume> VolumesAsyncCast()
    {
        await foreach (var volume in VolumesAsync)
        {
            yield return volume;
        }
    }

    public IAsyncEnumerable<IVolume> VolumesAsync => VolumesAsyncCast();

    public async ValueTask<IAsyncReader> ExtractAllEntriesAsync()
    {
        if (!IsSolid && Type != ArchiveType.SevenZip)
        {
            throw new SharpCompressException(
                "ExtractAllEntries can only be used on solid archives or 7Zip archives (which require random access)."
            );
        }
        await EnsureEntriesLoadedAsync();
        return await CreateReaderForSolidExtractionAsync();
    }

    public virtual ValueTask<bool> IsSolidAsync() => new(false);

    public async ValueTask<bool> IsCompleteAsync()
    {
        await EnsureEntriesLoadedAsync();
        return await EntriesAsync.AllAsync(x => x.IsComplete);
    }

    public async ValueTask<long> TotalSizeAsync() =>
        await EntriesAsync.AggregateAsync(0L, (total, cf) => total + cf.CompressedSize);

    public async ValueTask<long> TotalUncompressSizeAsync() =>
        await EntriesAsync.AggregateAsync(0L, (total, cf) => total + cf.Size);

    #endregion
}
