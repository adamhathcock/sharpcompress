using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal partial class SeekableZipFilePart : IDisposable
{
    private readonly SemaphoreSlim _asyncHeaderSemaphore = new(1, 1);
    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_isLocalHeaderLoaded)
        {
            await _asyncHeaderSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_isLocalHeaderLoaded)
                {
                    await LoadLocalHeaderAsync(cancellationToken).ConfigureAwait(false);
                    _isLocalHeaderLoaded = true;
                }
            }
            finally
            {
                _asyncHeaderSemaphore.Release();
            }
        }
        return await base.GetCompressedStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask LoadLocalHeaderAsync(CancellationToken cancellationToken = default)
    {
        // Use an independent stream for loading the header if multi-threading is enabled
        Stream streamToUse = BaseStream;
        bool disposeStream = false;

        if (
            BaseStream is SourceStream sourceStream
            && sourceStream.IsFileMode
            && sourceStream.ReaderOptions.EnableMultiThreadedExtraction
        )
        {
            var independentStream = sourceStream.CreateIndependentStream(0);
            if (independentStream is not null)
            {
                streamToUse = independentStream;
                disposeStream = true;
            }
        }
        else
        {
            // Check if BaseStream wraps a FileStream
            Stream? underlyingStream = BaseStream;
            if (BaseStream is IStreamStack streamStack)
            {
                underlyingStream = streamStack.BaseStream();
            }

            if (underlyingStream is FileStream fileStream)
            {
                streamToUse = new FileStream(
                    fileStream.Name,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                disposeStream = true;
            }
        }

        try
        {
            Header = await _headerFactory.GetLocalHeaderAsync(
                streamToUse,
                (DirectoryEntryHeader)Header
            ).ConfigureAwait(false);
        }
        finally
        {
            if (disposeStream)
            {
                if (streamToUse is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
#pragma warning disable VSTHRD103
                    streamToUse.Dispose();
#pragma warning restore VSTHRD103
                }
            }
        }
    }

    public void Dispose() => _asyncHeaderSemaphore.Dispose();

}
