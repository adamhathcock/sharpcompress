using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;

namespace SharpCompress.Readers;

/// <summary>
/// A generic push reader that reads unseekable comrpessed streams.
/// </summary>
public abstract class AbstractReader<TEntry, TVolume> : IReader, IReaderExtractionListener
    where TEntry : Entry
    where TVolume : Volume
{
    private bool _completed;
    private IEnumerator<TEntry>? _entriesForCurrentReadStream;
    private bool _wroteCurrentEntry;

    public event EventHandler<ReaderExtractionEventArgs<IEntry>>? EntryExtractionProgress;

    public event EventHandler<CompressedBytesReadEventArgs>? CompressedBytesRead;
    public event EventHandler<FilePartExtractionBeginEventArgs>? FilePartExtractionBegin;

    internal AbstractReader(ReaderOptions options, ArchiveType archiveType)
    {
        ArchiveType = archiveType;
        Options = options;
    }

    internal ReaderOptions Options { get; }

    public ArchiveType ArchiveType { get; }

    /// <summary>
    /// Current volume that the current entry resides in
    /// </summary>
    public abstract TVolume? Volume { get; }

    /// <summary>
    /// Current file entry
    /// </summary>
    public TEntry Entry => _entriesForCurrentReadStream.NotNull().Current;

    #region IDisposable Members

    public virtual void Dispose()
    {
        _entriesForCurrentReadStream?.Dispose();
        Volume?.Dispose();
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

        if (!Entry.IsSolid && Entry.CompressedSize > 0)
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

        if (writableStream is null)
        {
            throw new ArgumentNullException(nameof(writableStream));
        }
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
        var streamListener = this as IReaderExtractionListener;
        using Stream s = OpenEntryStream();
        s.TransferTo(writeStream, Entry, streamListener);
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
    protected EntryStream CreateEntryStream(Stream decompressed) => new(this, decompressed);

    protected virtual EntryStream GetEntryStream() =>
        CreateEntryStream(Entry.Parts.First().GetCompressedStream());

    #endregion

    IEntry IReader.Entry => Entry;

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

    void IReaderExtractionListener.FireEntryExtractionProgress(
        Entry entry,
        long bytesTransferred,
        int iterations
    ) =>
        EntryExtractionProgress?.Invoke(
            this,
            new ReaderExtractionEventArgs<IEntry>(
                entry,
                new ReaderProgress(entry, bytesTransferred, iterations)
            )
        );
}
