using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public override IAsyncEnumerable<TEntry> EntriesAsync => GetCurrentEntriesAsync();

    private async IAsyncEnumerable<TEntry> GetCurrentEntriesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var entry in OldEntriesAsync.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            yield return entry;
        }
        foreach (var entry in newEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!removedEntries.Contains(entry))
            {
                yield return entry;
            }
        }
    }

    private IAsyncEnumerable<TEntry> OldEntriesAsync =>
        LoadOriginalEntriesAsync().Where(x => !removedEntries.Contains(x));

    private async IAsyncEnumerable<TEntry> LoadOriginalEntriesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var index = 0;
        while (true)
        {
            while (index < originalEntries.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return originalEntries[index++];
            }
            if (originalEntriesLoaded)
            {
                yield break;
            }

            var generation = ++originalEntriesGeneration;
            await using var source = LoadEntriesAsync(VolumesAsync)
                .GetAsyncEnumerator(cancellationToken);
            for (var skipped = 0; skipped < index; skipped++)
            {
                if (!await source.MoveNextAsync().ConfigureAwait(false))
                {
                    throw new IncompleteArchiveException("Archive entries changed while loading.");
                }
            }
            while (
                generation == originalEntriesGeneration
                && await source.MoveNextAsync().ConfigureAwait(false)
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                originalEntries.Add(source.Current);
                index++;
                yield return source.Current;
            }
            if (generation == originalEntriesGeneration)
            {
                originalEntriesLoaded = true;
            }
        }
    }

    private async ValueTask RebuildModifiedCollectionAsync()
    {
        if (pauseRebuilding)
        {
            // Disposing the synchronous pause must not perform synchronous I/O for async callers.
            asyncRebuildPending = true;
            return;
        }
        hasModifications = true;
        asyncRebuildPending = false;
        newEntries.RemoveAll(v => removedEntries.Contains(v));
        modifiedEntries.Clear();
        await foreach (var entry in OldEntriesAsync.ConfigureAwait(false))
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
            var entry in OldEntriesAsync.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            if (entry.Key is { } path && KeysMatch(path, key))
            {
                return true;
            }
        }
        foreach (var entry in newEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!removedEntries.Contains(entry) && entry.Key is { } path && KeysMatch(path, key))
            {
                return true;
            }
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
        await RebuildModifiedCollectionAsync().ConfigureAwait(false);
        return entry;
    }

    public async ValueTask SaveToAsync(
        Stream stream,
        TOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var activeNewEntries = newEntries.Where(x => !removedEntries.Contains(x)).ToList();
        //reset streams of new entries
        activeNewEntries
            .Cast<IWritableArchiveEntry>()
            .ForEach(x => x.Stream.Seek(0, SeekOrigin.Begin));
        await SaveToAsync(stream, options, OldEntriesAsync, activeNewEntries, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        originalEntries.Cast<Entry>().ForEach(x => x.Close());
        newEntries.Cast<Entry>().ForEach(x => x.Close());
        removedEntries.Cast<Entry>().ForEach(x => x.Close());
        modifiedEntries.Cast<Entry>().ForEach(x => x.Close());
    }

    protected abstract ValueTask SaveToAsync(
        Stream stream,
        TOptions options,
        IAsyncEnumerable<TEntry> oldEntries,
        IEnumerable<TEntry> newEntries,
        CancellationToken cancellationToken = default
    );
}
