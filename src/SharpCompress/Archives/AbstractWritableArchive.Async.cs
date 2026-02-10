using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

public abstract partial class AbstractWritableArchive<TEntry, TVolume, TOptions>
    where TEntry : IArchiveEntry
    where TVolume : IVolume
    where TOptions : IWriterOptions
{
    // Async property moved from main file
    private IAsyncEnumerable<TEntry> OldEntriesAsync =>
        base.EntriesAsync.Where(x => !removedEntries.Contains(x));

    private async ValueTask RebuildModifiedCollectionAsync()
    {
        if (pauseRebuilding)
        {
            return;
        }
        hasModifications = true;
        newEntries.RemoveAll(v => removedEntries.Contains(v));
        modifiedEntries.Clear();
        await foreach (var entry in OldEntriesAsync)
        {
            modifiedEntries.Add(entry);
        }
        modifiedEntries.AddRange(newEntries);
    }

    public async ValueTask RemoveEntryAsync(TEntry entry)
    {
        if (!removedEntries.Contains(entry))
        {
            removedEntries.Add(entry);
            await RebuildModifiedCollectionAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> DoesKeyMatchExistingAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        await foreach (
            var entry in EntriesAsync.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            var path = entry.Key;
            if (path is null)
            {
                continue;
            }
            var p = path.Replace('/', '\\');
            if (p.Length > 0 && p[0] == '\\')
            {
                p = p.Substring(1);
            }
            return string.Equals(p, key, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public async ValueTask<TEntry> AddEntryAsync(
        string key,
        Stream source,
        bool closeStream,
        long size = 0,
        DateTime? modified = null,
        CancellationToken cancellationToken = default
    )
    {
        if (key.Length > 0 && key[0] is '/' or '\\')
        {
            key = key.Substring(1);
        }
        if (await DoesKeyMatchExistingAsync(key, cancellationToken).ConfigureAwait(false))
        {
            throw new ArchiveException("Cannot add entry with duplicate key: " + key);
        }
        var entry = CreateEntry(key, source, size, modified, closeStream);
        newEntries.Add(entry);
        await RebuildModifiedCollectionAsync().ConfigureAwait(false);
        return entry;
    }

    public async ValueTask<TEntry> AddDirectoryEntryAsync(
        string key,
        DateTime? modified = null,
        CancellationToken cancellationToken = default
    )
    {
        if (key.Length > 0 && key[0] is '/' or '\\')
        {
            key = key.Substring(1);
        }
        if (await DoesKeyMatchExistingAsync(key, cancellationToken).ConfigureAwait(false))
        {
            throw new ArchiveException("Cannot add entry with duplicate key: " + key);
        }
        var entry = CreateDirectoryEntry(key, modified);
        newEntries.Add(entry);
        await RebuildModifiedCollectionAsync();
        return entry;
    }

    public async ValueTask SaveToAsync(
        Stream stream,
        TOptions options,
        CancellationToken cancellationToken = default
    )
    {
        //reset streams of new entries
        newEntries.Cast<IWritableArchiveEntry>().ForEach(x => x.Stream.Seek(0, SeekOrigin.Begin));
        await SaveToAsync(stream, options, OldEntriesAsync, newEntries, cancellationToken)
            .ConfigureAwait(false);
    }

    protected abstract ValueTask SaveToAsync(
        Stream stream,
        TOptions options,
        IAsyncEnumerable<TEntry> oldEntries,
        IEnumerable<TEntry> newEntries,
        CancellationToken cancellationToken = default
    );
}
