using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Readers;

/// <summary>
/// A generic push reader that reads unseekable comrpessed streams.
/// </summary>
public abstract partial class AbstractReader<TEntry, TVolume> : IReader, IAsyncReader
    where TEntry : Entry
    where TVolume : Volume
{
    private bool _completed;
    private IEnumerator<TEntry>? _entriesForCurrentReadStream;
    private IAsyncEnumerator<TEntry>? _entriesForCurrentReadStreamAsync;
    private bool _wroteCurrentEntry;
    private readonly bool _disposeVolume;

    internal AbstractReader(
        ReaderOptions options,
        ArchiveType archiveType,
        bool disposeVolume = true
    )
    {
        ArchiveType = archiveType;
        _disposeVolume = disposeVolume;
        Options = options;
    }

    internal ReaderOptions Options { get; }

    public ArchiveType ArchiveType { get; }

    /// <summary>
    /// Current volume that the current entry resides in
    /// </summary>
    public abstract TVolume? Volume { get; }

    /// <summary>
    /// Current file entry (from either sync or async enumeration).
    /// </summary>
    public TEntry Entry
    {
        get
        {
            if (_entriesForCurrentReadStreamAsync is not null)
            {
                return _entriesForCurrentReadStreamAsync.Current;
            }
            return _entriesForCurrentReadStream.NotNull().Current;
        }
    }

    #region IDisposable Members

    public virtual void Dispose()
    {
        _entriesForCurrentReadStream?.Dispose();
        if (_disposeVolume)
        {
            Volume?.Dispose();
        }
    }

    #endregion

    public bool Cancelled { get; private set; }

    /// <summary>
    /// Indicates that the remaining entries are not required.
    /// On dispose of an EntryStream, the stream will not skip to the end of the entry.
    /// An attempt to move to the next entry will throw an exception, as the compressed stream is not positioned at an entry boundary.
    /// </summary>
    public void Cancel()
    {
        if (!_completed)
        {
            Cancelled = true;
        }
    }

    public bool MoveToNextEntry()
    {
        if (_entriesForCurrentReadStreamAsync is not null)
        {
            throw new ArchiveOperationException(
                $"{nameof(MoveToNextEntry)} cannot be used after {nameof(MoveToNextEntryAsync)} has been used."
            );
        }
        if (_completed)
        {
            return false;
        }
        if (Cancelled)
        {
            throw new ReaderCancelledException("Reader has been cancelled.");
        }
        if (_entriesForCurrentReadStream is null)
        {
            return LoadStreamForReading(RequestInitialStream());
        }
        if (!_wroteCurrentEntry)
        {
            SkipEntry();
        }
        _wroteCurrentEntry = false;
        if (NextEntryForCurrentStream())
        {
            return true;
        }
        _completed = true;
        return false;
    }

    protected bool LoadStreamForReading(Stream stream)
    {
        if (_entriesForCurrentReadStreamAsync is not null)
        {
            throw new ArchiveOperationException(
                $"{nameof(LoadStreamForReading)} cannot be used after {nameof(LoadStreamForReadingAsync)} has been used."
            );
        }
        _entriesForCurrentReadStream?.Dispose();
        if (stream is null || !stream.CanRead)
        {
            throw new MultipartStreamRequiredException(
                "File is split into multiple archives: '"
                    + Entry.Key
                    + "'. A new readable stream is required.  Use Cancel if it was intended."
            );
        }
        _entriesForCurrentReadStream = GetEntries(stream).GetEnumerator();
        return _entriesForCurrentReadStream.MoveNext();
    }

    protected virtual Stream RequestInitialStream() =>
        Volume.NotNull("Volume isn't loaded.").Stream;

    protected virtual ValueTask<Stream> RequestInitialStreamAsync(
        CancellationToken cancellationToken = default
    ) => new(RequestInitialStream());

    internal virtual bool NextEntryForCurrentStream() =>
        _entriesForCurrentReadStream.NotNull().MoveNext();

    protected abstract IEnumerable<TEntry> GetEntries(Stream stream);

    #region Entry Skip/Write

    private void SkipEntry()
    {
        if (!Entry.IsDirectory)
        {
            Skip();
        }
    }

    private void Skip()
    {
        var part = Entry.Parts.First();

        if (!Entry.IsSplitAfter && !Entry.IsSolid && Entry.CompressedSize > 0)
        {
            //not solid and has a known compressed size then we can skip raw bytes.
            var rawStream = part.GetRawStream();

            if (rawStream != null)
            {
                var bytesToAdvance = Entry.CompressedSize;
                rawStream.Skip(bytesToAdvance);
                part.Skipped = true;
                return;
            }
        }
        //don't know the size so we have to try to decompress to skip
        using var s = OpenEntryStream();
        s.SkipEntry();
    }

    public void WriteEntryTo(Stream writableStream)
    {
        if (_wroteCurrentEntry)
        {
            throw new ArgumentException("WriteEntryTo or OpenEntryStream can only be called once.");
        }

        ThrowHelper.ThrowIfNull(writableStream);
        if (!writableStream.CanWrite)
        {
            throw new ArgumentException(
                "A writable Stream was required.  Use Cancel if that was intended."
            );
        }

        Write(writableStream);
        _wroteCurrentEntry = true;
    }

    internal void Write(Stream writeStream)
    {
        using Stream s = OpenEntryStream();
        var sourceStream = WrapWithProgress(s, Entry);
        sourceStream.CopyTo(writeStream, Constants.BufferSize);
    }

    private Stream WrapWithProgress(Stream source, Entry entry)
    {
        var progress = Options.Progress;
        if (progress is null)
        {
            return source;
        }

        var entryPath = entry.Key ?? string.Empty;
        long? totalBytes = GetEntrySizeSafe(entry);
        return new ProgressReportingStream(
            source,
            progress,
            entryPath,
            totalBytes,
            leaveOpen: true
        );
    }

    private static long? GetEntrySizeSafe(Entry entry)
    {
        try
        {
            var size = entry.Size;
            // Return the actual size (including 0 for empty entries)
            // Negative values indicate unknown size
            return size >= 0 ? size : null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    public EntryStream OpenEntryStream()
    {
        if (_wroteCurrentEntry)
        {
            throw new ArgumentException("WriteEntryTo or OpenEntryStream can only be called once.");
        }
        var stream = GetEntryStream();
        _wroteCurrentEntry = true;
        return stream;
    }

    /// <summary>
    /// Retains a reference to the entry stream, so we can check whether it completed later.
    /// </summary>
    protected EntryStream CreateEntryStream(Stream? decompressed) =>
        new(this, decompressed.NotNull());

    protected virtual EntryStream GetEntryStream() =>
        CreateEntryStream(Entry.Parts.First().GetCompressedStream());

    #endregion

    IEntry IReader.Entry => Entry;
    IEntry IAsyncReader.Entry => Entry;
}
