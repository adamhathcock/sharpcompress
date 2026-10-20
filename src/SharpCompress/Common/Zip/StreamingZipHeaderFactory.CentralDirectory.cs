using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

/// <summary>
/// Encrypted entries written with a post data descriptor have no sizes in their local header, and the
/// descriptor cannot be found by scanning encrypted data. When the archive stream is seekable, the sizes
/// are taken from the central directory, walked by a forward-only cursor so memory use stays constant
/// regardless of the number of entries.
/// </summary>
internal sealed partial class StreamingZipHeaderFactory
{
    private const string EncryptedDataDescriptorNotSupported =
        "SharpCompress cannot currently read non-seekable Zip Streams with encrypted data that has been written in a non-seekable manner.";

    private IEnumerator<ZipHeader>? _centralDirectory;
    private IAsyncEnumerator<ZipHeader>? _centralDirectoryAsync;
    private bool _centralDirectoryUnavailable;
    private bool _centralDirectoryExhausted;
    private LocalEntryHeader? _resolvedEntry;
    private long _resolvedEntryDataEnd;
    private readonly Queue<DirectoryEntryHeader> _lookahead = new();
    private readonly int _lookaheadDepth;

    protected override bool TryResolveDescriptorSizes(ZipFileEntry entryHeader, Stream stream)
    {
        var seekableStream = GetSeekableStream(stream);
        if (
            _centralDirectoryUnavailable
            || seekableStream is null
            || entryHeader is not LocalEntryHeader localHeader
        )
        {
            return false;
        }

        var dataStart = seekableStream.Position;
        try
        {
            var directoryEntry = FindDirectoryEntry(seekableStream, localHeader);
            return directoryEntry is not null && Resolve(localHeader, directoryEntry, dataStart);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unreadable central directory leaves the forward-only behavior in place.
            _centralDirectoryUnavailable = true;
            ResetCentralDirectory();
            return false;
        }
        finally
        {
            seekableStream.Position = dataStart;
        }
    }

    protected override async ValueTask<bool> TryResolveDescriptorSizesAsync(
        ZipFileEntry entryHeader,
        Stream stream
    )
    {
        var seekableStream = GetSeekableStream(stream);
        if (
            _centralDirectoryUnavailable
            || seekableStream is null
            || entryHeader is not LocalEntryHeader localHeader
        )
        {
            return false;
        }

        var dataStart = seekableStream.Position;
        try
        {
            var directoryEntry = await FindDirectoryEntryAsync(seekableStream, localHeader)
                .ConfigureAwait(false);
            return directoryEntry is not null && Resolve(localHeader, directoryEntry, dataStart);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unreadable central directory leaves the forward-only behavior in place.
            _centralDirectoryUnavailable = true;
            await ResetCentralDirectoryAsync().ConfigureAwait(false);
            return false;
        }
        finally
        {
            seekableStream.Position = dataStart;
        }
    }

    private DirectoryEntryHeader? FindDirectoryEntry(Stream stream, LocalEntryHeader localHeader)
    {
        // The central directory normally lists entries in local header order, so the cursor only
        // moves forward; a single restart covers archives that order it differently.
        for (var pass = 0; pass < 2; pass++)
        {
            _centralDirectory ??= new SeekableZipHeaderFactory(null, ArchiveEncoding)
                .ReadSeekableHeader(stream)
                .GetEnumerator();
            FillLookahead(stream);
            while (_lookahead.Count > 0)
            {
                var directoryEntry = _lookahead.Dequeue();
                FillLookahead(stream);
                if (IsMatch(directoryEntry, localHeader))
                {
                    return directoryEntry;
                }
            }

            ResetCentralDirectory();
        }

        return null;
    }

    private async ValueTask<DirectoryEntryHeader?> FindDirectoryEntryAsync(
        Stream stream,
        LocalEntryHeader localHeader
    )
    {
        // The central directory normally lists entries in local header order, so the cursor only
        // moves forward; a single restart covers archives that order it differently.
        for (var pass = 0; pass < 2; pass++)
        {
            _centralDirectoryAsync ??= new SeekableZipHeaderFactory(null, ArchiveEncoding)
                .ReadSeekableHeaderAsync(stream)
                .GetAsyncEnumerator();
            await FillLookaheadAsync(stream).ConfigureAwait(false);
            while (_lookahead.Count > 0)
            {
                var directoryEntry = _lookahead.Dequeue();
                await FillLookaheadAsync(stream).ConfigureAwait(false);
                if (IsMatch(directoryEntry, localHeader))
                {
                    return directoryEntry;
                }
            }

            await ResetCentralDirectoryAsync().ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Reads central directory records ahead of the reader, up to the lookahead depth (one record
    /// unless WinZip AES keys are derived ahead of time).
    /// </summary>
    private void FillLookahead(Stream stream)
    {
        while (
            !_centralDirectoryExhausted
            && _lookahead.Count < _lookaheadDepth
            && _centralDirectory!.MoveNext()
        )
        {
            if (Enqueue(_centralDirectory.Current) is { } directoryEntry)
            {
                StartKeyDerivation(stream, directoryEntry);
            }
        }
    }

    private async ValueTask FillLookaheadAsync(Stream stream)
    {
        while (
            !_centralDirectoryExhausted
            && _lookahead.Count < _lookaheadDepth
            && await _centralDirectoryAsync!.MoveNextAsync().ConfigureAwait(false)
        )
        {
            if (Enqueue(_centralDirectoryAsync.Current) is { } directoryEntry)
            {
                await StartKeyDerivationAsync(stream, directoryEntry).ConfigureAwait(false);
            }
        }
    }

    private DirectoryEntryHeader? Enqueue(ZipHeader header)
    {
        switch (header)
        {
            case DirectoryEntryHeader directoryEntry:
                _lookahead.Enqueue(directoryEntry);
                return directoryEntry;
            case DirectoryEndHeader:
                // The central directory enumerator does not stop at the end record.
                _centralDirectoryExhausted = true;
                return null;
            default:
                return null;
        }
    }

    private static bool IsMatch(
        DirectoryEntryHeader directoryEntry,
        LocalEntryHeader localHeader
    ) => string.Equals(directoryEntry.Name, localHeader.Name, StringComparison.Ordinal);

    private bool Resolve(
        LocalEntryHeader localHeader,
        DirectoryEntryHeader directoryEntry,
        long dataStart
    )
    {
        localHeader.CompressedSize = directoryEntry.CompressedSize;
        localHeader.UncompressedSize = directoryEntry.UncompressedSize;
        localHeader.Crc = directoryEntry.Crc;
        localHeader.IsCrcAvailable = true;
        _resolvedEntry = localHeader;
        _resolvedEntryDataEnd = dataStart + directoryEntry.CompressedSize;
        return true;
    }

    private void ResetCentralDirectory()
    {
        _centralDirectory?.Dispose();
        _centralDirectory = null;
        _centralDirectoryExhausted = false;
        _lookahead.Clear();
    }

    private async ValueTask ResetCentralDirectoryAsync()
    {
        if (_centralDirectoryAsync is not null)
        {
            await _centralDirectoryAsync.DisposeAsync().ConfigureAwait(false);
        }
        _centralDirectoryAsync = null;
        _centralDirectoryExhausted = false;
        _lookahead.Clear();
    }

    /// <summary>
    /// Positions the stream at the data descriptor of an entry whose sizes came from the central
    /// directory; decoders may stop short of the entry end (e.g. the WinZip AES authentication code).
    /// </summary>
    private void SeekPastResolvedEntry(LocalEntryHeader lastEntryHeader, Stream stream)
    {
        if (ReferenceEquals(lastEntryHeader, _resolvedEntry))
        {
            stream.Position = _resolvedEntryDataEnd;
            _resolvedEntry = null;
        }
    }

    /// <summary>
    /// An encrypted directory resolved from the central directory carries only its encryption header,
    /// which is skipped together with the data descriptor.
    /// </summary>
    private bool IsResolvedDirectory(LocalEntryHeader localHeader) =>
        localHeader.IsDirectory && ReferenceEquals(localHeader, _resolvedEntry);

    /// <summary>
    /// Fails instead of silently ending the entry enumeration when an encrypted directory written with
    /// a data descriptor has payload of unknown size (the following entries could not be located).
    /// </summary>
    private static void ThrowIfUnreadableEncryptedDirectory(
        LocalEntryHeader localHeader,
        bool hasData
    )
    {
        if (
            hasData
            && localHeader.IsDirectory
            && FlagUtility.HasFlag(localHeader.Flags, HeaderFlags.Encrypted)
        )
        {
            throw new NotSupportedException(EncryptedDataDescriptorNotSupported);
        }
    }

    /// <summary>
    /// Returns the seekable stream underneath SharpCompress wrappers, or null when the archive can
    /// only be read forward (a buffered <see cref="SharpCompressStream"/> only seeks within its buffer).
    /// </summary>
    private static Stream? GetSeekableStream(Stream stream)
    {
        while (stream is SharpCompressStream { IsPassthrough: true } sharpCompressStream)
        {
            stream = sharpCompressStream.BaseStream();
        }

        if (stream is SeekableSharpCompressStream seekableSharpCompressStream)
        {
            stream = seekableSharpCompressStream.BaseStream();
        }

        return stream is SharpCompressStream ? null
            : stream.CanSeek ? stream
            : null;
    }
}
