namespace SharpCompress.Archive
{
    using SharpCompress;
    using SharpCompress.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    public abstract class AbstractWritableArchive<TEntry, TVolume> : AbstractArchive<TEntry, TVolume>, IWritableArchive, IArchive, IDisposable where TEntry: IArchiveEntry where TVolume: IVolume
    {
        private bool hasModifications;
        private readonly List<TEntry> modifiedEntries;
        private readonly List<TEntry> newEntries;
        private readonly List<TEntry> removedEntries;

        internal AbstractWritableArchive(ArchiveType type) : base(type)
        {
            this.newEntries = new List<TEntry>();
            this.removedEntries = new List<TEntry>();
            this.modifiedEntries = new List<TEntry>();
        }

        internal AbstractWritableArchive(ArchiveType type, Stream stream, Options options) : base(type, Utility.AsEnumerable<Stream>(stream), options, null)
        {
            this.newEntries = new List<TEntry>();
            this.removedEntries = new List<TEntry>();
            this.modifiedEntries = new List<TEntry>();
        }
        public TEntry AddEntry(string key, Stream source) {
            return AddEntry(key,source,0,null);
        }
        public TEntry AddEntry(string key, Stream source,  long size,  DateTime? modified)
        {
            return this.AddEntry(key, source, false, size, modified);
        }
        public TEntry AddEntry(string key, Stream source, bool closeStream) {
            return AddEntry(key,source,closeStream,0,null);
        }
        public TEntry AddEntry(string key, Stream source, bool closeStream,  long size,  DateTime? modified)
        {
            if (key.StartsWith("/") || key.StartsWith(@"\"))
            {
                key = key.Substring(1);
            }
            if (this.DoesKeyMatchExisting(key))
            {
                throw new ArchiveException("Cannot add entry with duplicate key: " + key);
            }
            TEntry item = this.CreateEntry(key, source, size, modified, closeStream);
            this.newEntries.Add(item);
            this.RebuildModifiedCollection();
            return item;
        }

        protected TEntry CreateEntry(string key, Stream source, long size, DateTime? modified, bool closeStream)
        {
            if (!(source.CanRead && source.CanSeek))
            {
                throw new ArgumentException("Streams must be readable and seekable to use the Writing Archive API");
            }
            return this.CreateEntryInternal(key, source, size, modified, closeStream);
        }

        protected abstract TEntry CreateEntryInternal(string key, Stream source, long size, DateTime? modified, bool closeStream);
        public override void Dispose()
        {
            base.Dispose();
            Utility.ForEach<Entry>(Enumerable.Cast<Entry>(this.newEntries), delegate (Entry x) {
                x.Close();
            });
            Utility.ForEach<Entry>(Enumerable.Cast<Entry>(this.removedEntries), delegate (Entry x) {
                x.Close();
            });
            Utility.ForEach<Entry>(Enumerable.Cast<Entry>(this.modifiedEntries), delegate (Entry x) {
                x.Close();
            });
        }

        private bool DoesKeyMatchExisting(string key)
        {
            foreach (string str in Enumerable.Select<TEntry, string>(this.Entries, delegate (TEntry x) {
                return x.Key;
            }))
            {
                string a = str.Replace('/', '\\');
                if (a.StartsWith(@"\"))
                {
                    a = a.Substring(1);
                }
                return string.Equals(a, key, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void RebuildModifiedCollection()
        {
            this.hasModifications = true;
            this.newEntries.RemoveAll(delegate (TEntry v) {
                return removedEntries.Contains(v);
            });
            this.modifiedEntries.Clear();
            this.modifiedEntries.AddRange(Enumerable.Concat<TEntry>(this.OldEntries, this.newEntries));
        }

        public void RemoveEntry(TEntry entry)
        {
            if (!this.removedEntries.Contains(entry))
            {
                this.removedEntries.Add(entry);
                this.RebuildModifiedCollection();
            }
        }

        public void SaveTo(Stream stream, CompressionInfo compressionType)
        {
            Utility.ForEach<IWritableArchiveEntry>(Enumerable.Cast<IWritableArchiveEntry>(this.newEntries), delegate (IWritableArchiveEntry x) {
                x.Stream.Seek(0L, SeekOrigin.Begin);
            });
            this.SaveTo(stream, compressionType, this.OldEntries, this.newEntries);
        }

        protected abstract void SaveTo(Stream stream, CompressionInfo compressionType, IEnumerable<TEntry> oldEntries, IEnumerable<TEntry> newEntries);
        IArchiveEntry IWritableArchive.AddEntry(string key, Stream source, bool closeStream, long size, DateTime? modified)
        {
            return this.AddEntry(key, source, closeStream, size, modified);
        }

        void IWritableArchive.RemoveEntry(IArchiveEntry entry)
        {
            this.RemoveEntry((TEntry) entry);
        }

        public override ICollection<TEntry> Entries
        {
            get
            {
                if (this.hasModifications)
                {
                    return this.modifiedEntries;
                }
                return base.Entries;
            }
        }

        private IEnumerable<TEntry> OldEntries
        {
            get
            {
                return Enumerable.Where<TEntry>(base.Entries, delegate (TEntry x) {
                    return !removedEntries.Contains(x);
                });
            }
        }
    }
}

