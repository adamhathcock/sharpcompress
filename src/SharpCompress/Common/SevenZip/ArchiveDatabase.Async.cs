using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;

namespace SharpCompress.Common.SevenZip;

internal sealed partial class ArchiveDatabase
{
    internal async ValueTask<Stream> GetFolderStreamAsync(
        Stream stream,
        CFolder folder,
        IPasswordProvider pw,
        CancellationToken cancellationToken
    )
    {
        var packStreamIndex = folder._firstPackStreamId;
        var folderStartPackPos = GetFolderStreamPos(folder, 0);
        var count = folder._packStreams.Count;
        var packSizes = new long[count];
        for (var j = 0; j < count; j++)
        {
            packSizes[j] = _packSizes[packStreamIndex + j];
        }

        return await DecoderStreamHelper
            .CreateDecoderStreamAsync(
                stream,
                folderStartPackPos,
                packSizes,
                folder,
                pw,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Async counterpart of the caching <c>GetFolderStream</c> overload. Reuses a cached decoder
    /// stream for the folder when possible instead of recreating and re-decoding from the start.
    /// </summary>
    internal async ValueTask<Stream> GetFolderStreamAsync(
        Stream stream,
        CFolder folder,
        IPasswordProvider pw,
        long skipSize,
        long entrySize,
        CancellationToken cancellationToken
    )
    {
        if (_cachedFolder == folder && _cachedFolderStream != null)
        {
            if (skipSize >= _cachedFolderStreamPosition)
            {
                var delta = skipSize - _cachedFolderStreamPosition;
                if (delta > 0)
                {
                    await _cachedFolderStream
                        .SkipAsync(delta, cancellationToken)
                        .ConfigureAwait(false);
                }
                // Assume the caller will fully consume the returned entry stream, advancing the
                // shared stream by entrySize bytes.
                _cachedFolderStreamPosition = skipSize + entrySize;
                return _cachedFolderStream;
            }

            // Non-sequential (backward) access within the same folder requires restarting.
            await DisposeCachedFolderStreamAsync().ConfigureAwait(false);
        }
        else if (_cachedFolderStream != null)
        {
            await DisposeCachedFolderStreamAsync().ConfigureAwait(false);
        }

        var newStream = await GetFolderStreamAsync(stream, folder, pw, cancellationToken)
            .ConfigureAwait(false);
        if (skipSize > 0)
        {
            await newStream.SkipAsync(skipSize, cancellationToken).ConfigureAwait(false);
        }

        _cachedFolder = folder;
        _cachedFolderStream = newStream;
        _cachedFolderStreamPosition = skipSize + entrySize;
        return newStream;
    }

    private async ValueTask DisposeCachedFolderStreamAsync()
    {
        if (_cachedFolderStream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
#pragma warning disable VSTHRD103 // Fallback for streams that do not support async disposal.
            _cachedFolderStream?.Dispose();
#pragma warning restore VSTHRD103
        }
        _cachedFolderStream = null;
        _cachedFolder = null;
    }
}
