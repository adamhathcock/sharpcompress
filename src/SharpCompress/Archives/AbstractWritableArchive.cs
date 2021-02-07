using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Archives
{
    public abstract class AbstractWritableArchive<TEntry, TVolume> : AbstractArchive<TEntry, TVolume>, IWritableArchive
        where TEntry : IArchiveEntry
        where TVolume : IVolume
    {
        private class RebuildPauseDisposable : IAsyncDisposable
        {
            private readonly AbstractWritableArchive<TEntry, TVolume> archive;

            public RebuildPauseDisposable(AbstractWritableArchive<TEntry, TVolume> archive)
            {
                this.archive = archive;
                archive.pauseRebuilding = true;
            }

            public async ValueTask DisposeAsync()
            {
                archive.pauseRebuilding = false;
                await archive.RebuildModifiedCollection();
            }
        }
        private readonly List<TEntry> newEntries = new();
        private readonly List<TEntry> removedEntries = new();

        private readonly List<TEntry> modifiedEntries = new();
        private bool hasModifications;
        private bool pauseRebuilding;

        internal AbstractWritableArchive(ArchiveType type)
            : base(type)
        {
        }

        internal AbstractWritableArchive(ArchiveType type, Stream stream, ReaderOptions readerFactoryOptions,
                                         CancellationToken cancellationToken)
            : base(type, stream.AsAsyncEnumerable(), readerFactoryOptions, cancellationToken)
        {
        }

        internal AbstractWritableArchive(ArchiveType type, FileInfo fileInfo, ReaderOptions readerFactoryOptions,
                                         CancellationToken cancellationToken)
            : base(type, fileInfo, readerFactoryOptions, cancellationToken)
        {
        }

        public override IAsyncEnumerable<TEntry> Entries
        {
            get
            {
                if (hasModifications)
                {
                    return modifiedEntries.ToAsyncEnumerable();
                }
                return base.Entries;
            }
        }

        public IAsyncDisposable PauseEntryRebuilding()
        {
            return new RebuildPauseDisposable(this);
        }

        private async ValueTask RebuildModifiedCollection()
        {
            if (pauseRebuilding)
            {
                return;
            }
            hasModifications = true;
            newEntries.RemoveAll(v => removedEntries.Contains(v));
            modifiedEntries.Clear();
            modifiedEntries.AddRange(await OldEntries.Concat(newEntries.ToAsyncEnumerable()).ToListAsync());
        }

        private IAsyncEnumerable<TEntry> OldEntries { get { return base.Entries.Where(x => !removedEntries.Contains(x)); } }

        public async ValueTask RemoveEntryAsync(TEntry entry)
        {
            if (!removedEntries.Contains(entry))
            {
                removedEntries.Add(entry);
                await RebuildModifiedCollection();
            }
        }

        ValueTask IWritableArchive.RemoveEntryAsync(IArchiveEntry entry, CancellationToken cancellationToken)
        {
            return RemoveEntryAsync((TEntry)entry);
        }

        public ValueTask<TEntry>  AddEntryAsync(string key, Stream source,
                                                       long size = 0, DateTime? modified = null, 
                                                       CancellationToken cancellationToken = default)
        {
            return AddEntryAsync(key, source, false, size, modified, cancellationToken);
        }

        async ValueTask<IArchiveEntry> IWritableArchive.AddEntryAsync(string key, Stream source, bool closeStream, long size, DateTime? modified, CancellationToken cancellationToken)
        {
            return await AddEntryAsync(key, source, closeStream, size, modified, cancellationToken);
        }

        public async ValueTask<TEntry> AddEntryAsync(string key, Stream source, bool closeStream,
                                    long size = 0, DateTime? modified = null, CancellationToken cancellationToken = default)
        {
            if (key.Length > 0 && key[0] is '/' or '\\')
            {
                key = key.Substring(1);
            }
            if (await DoesKeyMatchExisting(key))
            {
                throw new ArchiveException("Cannot add entry with duplicate key: " + key);
            }
            var entry = await CreateEntry(key, source, size, modified, closeStream, cancellationToken);
            newEntries.Add(entry);
            await RebuildModifiedCollection();
            return entry;
        }

        private async ValueTask<bool> DoesKeyMatchExisting(string key)
        {
            await foreach (var path in Entries.Select(x => x.Key))
            {
                var p = path.Replace('/', '\\');
                if (p.Length > 0 && p[0] == '\\')
                {
                    p = p.Substring(1);
                }
                return string.Equals(p, key, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public async ValueTask SaveToAsync(Stream stream, WriterOptions options, CancellationToken cancellationToken = default)
        {
            //reset streams of new entries
            newEntries.Cast<IWritableArchiveEntry>().ForEach(x => x.Stream.Seek(0, SeekOrigin.Begin));
            await SaveToAsync(stream, options, OldEntries, newEntries.ToAsyncEnumerable(), cancellationToken);
        }

        protected ValueTask<TEntry> CreateEntry(string key, Stream source, long size, DateTime? modified,
                                                bool closeStream, CancellationToken cancellationToken)
        {
            if (!source.CanRead || !source.CanSeek)
            {
                throw new ArgumentException("Streams must be readable and seekable to use the Writing Archive API");
            }
            return CreateEntryInternal(key, source, size, modified, closeStream, cancellationToken);
        }

        protected abstract ValueTask<TEntry> CreateEntryInternal(string key, Stream source, long size, DateTime? modified,
                                                      bool closeStream, CancellationToken cancellationToken);

        protected abstract ValueTask SaveToAsync(Stream stream, WriterOptions options, IAsyncEnumerable<TEntry> oldEntries, IAsyncEnumerable<TEntry> newEntries,
                                                 CancellationToken cancellationToken = default);

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            newEntries.Cast<Entry>().ForEach(x => x.Close());
            removedEntries.Cast<Entry>().ForEach(x => x.Close());
            modifiedEntries.Cast<Entry>().ForEach(x => x.Close());
        }
    }
}
