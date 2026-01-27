using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public abstract partial class AbstractArchive<TEntry, TVolume>
    where TEntry : IArchiveEntry
    where TVolume : IVolume
{
    #region Async Support

    // Async properties
    public virtual IAsyncEnumerable<TEntry> EntriesAsync => _lazyEntriesAsync;

    public IAsyncEnumerable<TVolume> VolumesAsync => _lazyVolumesAsync;

    protected virtual async IAsyncEnumerable<TEntry> LoadEntriesAsync(
        IAsyncEnumerable<TVolume> volumes
    )
    {
        foreach (var item in LoadEntries(await volumes.ToListAsync()))
        {
            yield return item;
        }
    }

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

    private async IAsyncEnumerable<IArchiveEntry> EntriesAsyncCast()
    {
        await foreach (var entry in EntriesAsync)
        {
            yield return entry;
        }
    }

    IAsyncEnumerable<IArchiveEntry> IAsyncArchive.EntriesAsync => EntriesAsyncCast();

    IAsyncEnumerable<IVolume> IAsyncArchive.VolumesAsync => VolumesAsyncCast();

    private async IAsyncEnumerable<IVolume> VolumesAsyncCast()
    {
        await foreach (var volume in _lazyVolumesAsync)
        {
            yield return volume;
        }
    }

    public async ValueTask<IAsyncReader> ExtractAllEntriesAsync()
    {
        if (!await IsSolidAsync() && Type != ArchiveType.SevenZip)
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

    public async ValueTask<long> TotalUncompressedSizeAsync() =>
        await EntriesAsync.AggregateAsync(0L, (total, cf) => total + cf.Size);

    public ValueTask<bool> IsEncryptedAsync() => new(IsEncrypted);

    #endregion
}
