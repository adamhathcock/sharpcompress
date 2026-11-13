using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.IO;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public abstract partial class AbstractWritableArchive<TEntry, TVolume, TOptions>
    : AbstractArchive<TEntry, TVolume>,
        IWritableArchive<TOptions>,
        IWritableAsyncArchive<TOptions>
    where TEntry : IArchiveEntry
    where TVolume : IVolume
    where TOptions : IWriterOptions
{
    private class RebuildPauseDisposable : IDisposable
    {
        private readonly AbstractWritableArchive<TEntry, TVolume, TOptions> archive;

        public RebuildPauseDisposable(AbstractWritableArchive<TEntry, TVolume, TOptions> archive)
        {
            this.archive = archive;
            archive.pauseRebuilding = true;
        }

        public void Dispose()
        {
            archive.pauseRebuilding = false;
            if (!archive.asyncRebuildPending)
            {
                archive.RebuildModifiedCollection();
            }
        }
    }

    private readonly List<TEntry> newEntries = new();
    private readonly List<TEntry> removedEntries = new();
    private readonly List<TEntry> originalEntries = new();
    private readonly LazyReadOnlyCollection<TEntry> originalEntriesView;
    private bool originalEntriesLoaded;
    private IEnumerator<TEntry>? originalEntriesSource;
    private IAsyncEnumerator<TEntry>? originalEntriesAsyncSource;
    private int originalEntriesRead;
    private int originalEntriesAsyncRead;

    private readonly List<TEntry> modifiedEntries = new();
    private bool hasModifications;
    private bool pauseRebuilding;
    private bool asyncRebuildPending;

    internal AbstractWritableArchive(ArchiveType type)
        : base(type)
    {
        originalEntriesLoaded = true;
        originalEntriesView = new LazyReadOnlyCollection<TEntry>(LoadOriginalEntries());
    }

    internal AbstractWritableArchive(ArchiveType type, SourceStream sourceStream)
        : base(type, sourceStream)
    {
        originalEntriesView = new LazyReadOnlyCollection<TEntry>(LoadOriginalEntries());
    }

    public override ICollection<TEntry> Entries
    {
        get
        {
            if (asyncRebuildPending && !pauseRebuilding)
            {
                RebuildModifiedCollection();
            }
            if (hasModifications)
            {
                return modifiedEntries;
            }
            return originalEntriesView;
        }
    }

    public IDisposable PauseEntryRebuilding() => new RebuildPauseDisposable(this);

    private void RebuildModifiedCollection()
    {
        if (pauseRebuilding)
        {
            return;
        }
        hasModifications = true;
        asyncRebuildPending = false;
        newEntries.RemoveAll(v => removedEntries.Contains(v));
        modifiedEntries.Clear();
        modifiedEntries.AddRange(OldEntries.Concat(newEntries));
    }

    private IEnumerable<TEntry> LoadOriginalEntries()
    {
        var index = 0;
        while (true)
        {
            while (index < originalEntries.Count)
            {
                yield return originalEntries[index++];
            }
            if (originalEntriesLoaded)
            {
                yield break;
            }

            // Resume the base cache instead of reparsing its prefix on every API
            // switch. Each loader scans at most once; only the first entry object
            // seen at each index is exposed to callers and removal checks.
            originalEntriesSource ??= base.Entries.GetEnumerator();
            while (originalEntriesSource.MoveNext())
            {
                if (originalEntriesRead++ < originalEntries.Count)
                {
                    continue;
                }
                originalEntries.Add(originalEntriesSource.Current);
                break;
            }
            if (index == originalEntries.Count)
            {
                originalEntriesSource.Dispose();
                originalEntriesSource = null;
                originalEntriesLoaded = true;
            }
        }
    }

    private IEnumerable<TEntry> OldEntries =>
        originalEntriesView.Where(x => !removedEntries.Contains(x));

    public void RemoveEntry(TEntry entry)
    {
        if (!removedEntries.Contains(entry))
        {
            removedEntries.Add(entry);
            RebuildModifiedCollection();
        }
    }

    void IWritableArchive.RemoveEntry(IArchiveEntry entry) => RemoveEntry((TEntry)entry);

    public TEntry AddEntry(string key, Stream source, long size = 0, DateTime? modified = null) =>
        AddEntry(key, source, false, size, modified);

    IArchiveEntry IWritableArchive.AddEntry(
        string key,
        Stream source,
        bool closeStream,
        long size,
        DateTime? modified
    ) => AddEntry(key, source, closeStream, size, modified);

    IArchiveEntry IWritableArchive.AddDirectoryEntry(string key, DateTime? modified) =>
        AddDirectoryEntry(key, modified);

    public TEntry AddEntry(
        string key,
        Stream source,
        bool closeStream,
        long size = 0,
        DateTime? modified = null
    )
    {
        if (key.Length > 0 && key[0] is '/' or '\\')
        {
            key = key.Substring(1);
        }
        if (DoesKeyMatchExisting(key))
        {
            throw new ArchiveException("Cannot add entry with duplicate key: " + key);
        }
        var entry = CreateEntry(key, source, size, modified, closeStream);
        newEntries.Add(entry);
        RebuildModifiedCollection();
        return entry;
    }

    private bool DoesKeyMatchExisting(string key)
    {
        foreach (
            var path in OldEntries
                .Concat(newEntries)
                .Where(x => !removedEntries.Contains(x))
                .Select(x => x.Key)
        )
        {
            if (path is not null && KeysMatch(path, key))
            {
                return true;
            }
        }
        return false;
    }

    private bool KeysMatch(string left, string right) =>
        string.Equals(NormalizeKey(left), NormalizeKey(right), StringComparison.OrdinalIgnoreCase);

    private string NormalizeKey(string key)
    {
        key = key.Replace('\\', '/');
        if (Type is ArchiveType.Zip or ArchiveType.Tar)
        {
            // ZIP and TAR writers remove drive prefixes and surrounding slashes from names.
#if LEGACY_DOTNET
            var colon = key.IndexOf(':');
#else
            var colon = key.IndexOf(':', StringComparison.Ordinal);
#endif
            if (colon >= 0)
            {
                key = key.Substring(colon + 1);
            }
            return key.Trim('/');
        }
        return key.Length > 0 && key[0] == '/' ? key.Substring(1) : key;
    }

    ValueTask IWritableAsyncArchive.RemoveEntryAsync(IArchiveEntry entry) =>
        RemoveEntryAsync((TEntry)entry);

    async ValueTask<IArchiveEntry> IWritableAsyncArchive.AddEntryAsync(
        string key,
        Stream source,
        bool closeStream,
        long size,
        DateTime? modified,
        CancellationToken cancellationToken
    ) =>
        await AddEntryAsync(key, source, closeStream, size, modified, cancellationToken)
            .ConfigureAwait(false);

    async ValueTask<IArchiveEntry> IWritableAsyncArchive.AddDirectoryEntryAsync(
        string key,
        DateTime? modified,
        CancellationToken cancellationToken
    ) => await AddDirectoryEntryAsync(key, modified, cancellationToken).ConfigureAwait(false);

    public TEntry AddDirectoryEntry(string key, DateTime? modified = null)
    {
        if (key.Length > 0 && key[0] is '/' or '\\')
        {
            key = key.Substring(1);
        }
        if (DoesKeyMatchExisting(key))
        {
            throw new ArchiveException("Cannot add entry with duplicate key: " + key);
        }
        var entry = CreateDirectoryEntry(key, modified);
        newEntries.Add(entry);
        RebuildModifiedCollection();
        return entry;
    }

    public void SaveTo(Stream stream, TOptions options)
    {
        var activeNewEntries = newEntries.Where(x => !removedEntries.Contains(x)).ToList();
        //reset streams of new entries
        activeNewEntries
            .Cast<IWritableArchiveEntry>()
            .ForEach(x => x.Stream.Seek(0, SeekOrigin.Begin));
        SaveTo(stream, options, OldEntries, activeNewEntries);
    }

    protected TEntry CreateEntry(
        string key,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    )
    {
        if (!source.CanRead || !source.CanSeek)
        {
            throw new ArchiveException(
                "Streams must be readable and seekable to use the Writing Archive API"
            );
        }
        return CreateEntryInternal(key, source, size, modified, closeStream);
    }

    protected abstract TEntry CreateEntryInternal(
        string key,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    );

    protected abstract TEntry CreateDirectoryEntry(string key, DateTime? modified);

    protected abstract void SaveTo(
        Stream stream,
        TOptions options,
        IEnumerable<TEntry> oldEntries,
        IEnumerable<TEntry> newEntries
    );

    public override void Dispose()
    {
        base.Dispose();
        originalEntriesSource?.Dispose();
        originalEntries.Cast<Entry>().ForEach(x => x.Close());
        newEntries.Cast<Entry>().ForEach(x => x.Close());
        removedEntries.Cast<Entry>().ForEach(x => x.Close());
        modifiedEntries.Cast<Entry>().ForEach(x => x.Close());
    }
}
