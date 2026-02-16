using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public abstract partial class AbstractReader<TEntry, TVolume>
    where TEntry : Entry
    where TVolume : Volume
{
    public virtual async ValueTask DisposeAsync()
    {
        if (_entriesForCurrentReadStreamAsync is not null)
        {
            await _entriesForCurrentReadStreamAsync.DisposeAsync().ConfigureAwait(false);
        }

        // If Volume implements IAsyncDisposable, use async disposal
        if (Volume is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            Volume?.Dispose();
        }
    }

    public async ValueTask<bool> MoveToNextEntryAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return false;
        }
        if (Cancelled)
        {
            throw new ReaderCancelledException("Reader has been cancelled.");
        }
        if (_entriesForCurrentReadStreamAsync is null)
        {
            return await LoadStreamForReadingAsync(
                    await RequestInitialStreamAsync(cancellationToken).ConfigureAwait(false)
                )
                .ConfigureAwait(false);
        }
        if (!_wroteCurrentEntry)
        {
            await SkipEntryAsync(cancellationToken).ConfigureAwait(false);
        }
        _wroteCurrentEntry = false;
        if (await NextEntryForCurrentStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }
        _completed = true;
        return false;
    }

    protected async ValueTask<bool> LoadStreamForReadingAsync(Stream stream)
    {
        if (_entriesForCurrentReadStreamAsync is not null)
        {
            await _entriesForCurrentReadStreamAsync.DisposeAsync().ConfigureAwait(false);
        }
        if (stream is null || !stream.CanRead)
        {
            throw new MultipartStreamRequiredException(
                "File is split into multiple archives: '"
                    + Entry.Key
                    + "'. A new readable stream is required.  Use Cancel if it was intended."
            );
        }
        _entriesForCurrentReadStreamAsync = GetEntriesAsync(stream).GetAsyncEnumerator();
        return await _entriesForCurrentReadStreamAsync.MoveNextAsync().ConfigureAwait(false);
    }

    private async ValueTask SkipEntryAsync(CancellationToken cancellationToken)
    {
        if (!Entry.IsDirectory)
        {
            await SkipAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SkipAsync(CancellationToken cancellationToken)
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
#if LEGACY_DOTNET
        using var s = await OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        await s.SkipEntryAsync(cancellationToken).ConfigureAwait(false);
#else
        await using var s = await OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        await s.SkipEntryAsync(cancellationToken).ConfigureAwait(false);
#endif
    }

    public async ValueTask WriteEntryToAsync(
        Stream writableStream,
        CancellationToken cancellationToken = default
    )
    {
        if (_wroteCurrentEntry)
        {
            throw new ArgumentException(
                "WriteEntryToAsync or OpenEntryStreamAsync can only be called once."
            );
        }

        ThrowHelper.ThrowIfNull(writableStream);
        if (!writableStream.CanWrite)
        {
            throw new ArgumentException(
                "A writable Stream was required.  Use Cancel if that was intended."
            );
        }

        await WriteAsync(writableStream, cancellationToken).ConfigureAwait(false);
        _wroteCurrentEntry = true;
    }

    internal async ValueTask WriteAsync(Stream writeStream, CancellationToken cancellationToken)
    {
#if LEGACY_DOTNET
        using Stream s = await OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        var sourceStream = WrapWithProgress(s, Entry);
        await sourceStream
            .CopyToAsync(writeStream, Constants.BufferSize, cancellationToken)
            .ConfigureAwait(false);
#else
        await using Stream s = await OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
        var sourceStream = WrapWithProgress(s, Entry);
        await sourceStream
            .CopyToAsync(writeStream, Constants.BufferSize, cancellationToken)
            .ConfigureAwait(false);
#endif
    }

    public async ValueTask<EntryStream> OpenEntryStreamAsync(
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

    protected virtual async ValueTask<EntryStream> GetEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        var stream = await Entry
            .Parts.First()
            .GetCompressedStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return CreateEntryStream(stream);
    }

    internal virtual ValueTask<bool> NextEntryForCurrentStreamAsync() =>
        _entriesForCurrentReadStreamAsync.NotNull().MoveNextAsync();

    /// <summary>
    /// Moves the current async enumerator to the next entry.
    /// </summary>
    internal virtual ValueTask<bool> NextEntryForCurrentStreamAsync(
        CancellationToken cancellationToken
    )
    {
        if (_entriesForCurrentReadStreamAsync is not null)
        {
            return _entriesForCurrentReadStreamAsync.MoveNextAsync();
        }

        return new ValueTask<bool>(NextEntryForCurrentStream());
    }

    // Async iterator method
    protected virtual async IAsyncEnumerable<TEntry> GetEntriesAsync(Stream stream)
    {
        foreach (var entry in GetEntries(stream))
        {
            yield return entry;
        }
    }
}
