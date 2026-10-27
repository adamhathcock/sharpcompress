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
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tracker = await GetCachedFolderTrackerAsync(stream, folder, pw, cancellationToken)
            .ConfigureAwait(false);
        if (tracker.IsFaulted || tracker.IsAsync == false || skipSize < tracker.Position)
        {
            // Failed reads and mode changes invalidate reuse, as does backward access.
            await DisposeCachedFolderStreamAsync().ConfigureAwait(false);
            tracker = await GetCachedFolderTrackerAsync(stream, folder, pw, cancellationToken)
                .ConfigureAwait(false);
        }

        var delta = skipSize - tracker.Position;
        if (delta > 0)
        {
            await tracker.SkipAsync(delta, cancellationToken).ConfigureAwait(false);
        }

        return tracker;
    }

    private async ValueTask<FolderStreamTracker> GetCachedFolderTrackerAsync(
        Stream stream,
        CFolder folder,
        IPasswordProvider pw,
        CancellationToken cancellationToken
    )
    {
        if (_cachedFolderStream is null || _cachedFolder != folder)
        {
            await DisposeCachedFolderStreamAsync().ConfigureAwait(false);
            _cachedFolderStream = await GetFolderStreamAsync(stream, folder, pw, cancellationToken)
                .ConfigureAwait(false);
            _cachedFolderTracker = new FolderStreamTracker(_cachedFolderStream);
            _cachedFolder = folder;
        }

        return _cachedFolderTracker.NotNull();
    }

    internal async ValueTask DisposeCachedFolderStreamAsync()
    {
        var stream = _cachedFolderStream;
        _cachedFolderStream = null;
        _cachedFolderTracker = null;
        _cachedFolder = null;
        if (stream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
#pragma warning disable VSTHRD103 // Fallback for streams that do not support async disposal.
            stream?.Dispose();
#pragma warning restore VSTHRD103
        }
    }
}
