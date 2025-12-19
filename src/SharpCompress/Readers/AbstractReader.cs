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
public abstract class AbstractReader<TEntry, TVolume> : IReader
    where TEntry : Entry
    where TVolume : Volume
{
    private bool _completed;
    private IEnumerator<TEntry>? _entriesForCurrentReadStream;
    private bool _wroteCurrentEntry;

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

    public async Task<bool> MoveToNextEntryAsync(CancellationToken cancellationToken = default)
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
            await SkipEntryAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task SkipEntryAsync(CancellationToken cancellationToken)
    {
        if (!Entry.IsDirectory)
        {
            await SkipAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task SkipAsync(CancellationToken cancellationToken)
    {
        var part = Entry.Parts.First();

        if (!Entry.IsSplitAfter && !Entry.IsSolid && Entry.CompressedSize > 0)
        {
            //not solid and has a known compressed size then we can skip raw bytes.
            var rawStream = part.GetRawStream();

            if (rawStream != null)
            {
                var bytesToAdvance = Entry.CompressedSize;
                await rawStream.SkipAsync(bytesToAdvance, cancellationToken).ConfigureAwait(false);
                part.Skipped = true;
                return;
            }
        }
        //don't know the size so we have to try to decompress to skip
#if NETFRAMEWORK || NETSTANDARD2_0
        using var s = await OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        await s.SkipEntryAsync(cancellationToken).ConfigureAwait(false);
#else
        await using var s = await OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        await s.SkipEntryAsync(cancellationToken).ConfigureAwait(false);
#endif
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

    public async Task WriteEntryToAsync(
        Stream writableStream,
        CancellationToken cancellationToken = default
    )
    {
        if (_wroteCurrentEntry)
        {
            throw new ArgumentException(
                "WriteEntryToAsync or OpenEntryStream can only be called once."
            );
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

        await WriteAsync(writableStream, cancellationToken).ConfigureAwait(false);
        _wroteCurrentEntry = true;
    }

    internal void Write(Stream writeStream)
    {
        using Stream s = OpenEntryStream();
        var sourceStream = WrapWithProgress(s, Entry);
        sourceStream.CopyTo(writeStream, 81920);
    }

    internal async Task WriteAsync(Stream writeStream, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        using Stream s = OpenEntryStream();
        var sourceStream = WrapWithProgress(s, Entry);
        await sourceStream.CopyToAsync(writeStream, 81920, cancellationToken).ConfigureAwait(false);
#else
        await using Stream s = OpenEntryStream();
        var sourceStream = WrapWithProgress(s, Entry);
        await sourceStream.CopyToAsync(writeStream, 81920, cancellationToken).ConfigureAwait(false);
#endif
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

    public async Task<EntryStream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_wroteCurrentEntry)
        {
            throw new ArgumentException(
                "WriteEntryToAsync or OpenEntryStreamAsync can only be called once."
            );
        }
        var stream = await GetEntryStreamAsync(cancellationToken).ConfigureAwait(false);
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

    protected virtual Task<EntryStream> GetEntryStreamAsync(
        CancellationToken cancellationToken = default
    ) => Task.FromResult(GetEntryStream());

    #endregion

    IEntry IReader.Entry => Entry;
}
