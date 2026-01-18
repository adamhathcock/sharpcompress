using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal class SeekableZipFilePart : ZipFilePart
{
    private volatile bool _isLocalHeaderLoaded;
    private readonly SeekableZipHeaderFactory _headerFactory;
    private readonly object _headerLock = new();
    private readonly SemaphoreSlim _asyncHeaderSemaphore = new(1, 1);

    internal SeekableZipFilePart(
        SeekableZipHeaderFactory headerFactory,
        DirectoryEntryHeader header,
        Stream stream
    )
        : base(header, stream) => _headerFactory = headerFactory;

    internal override Stream GetCompressedStream()
    {
        if (!_isLocalHeaderLoaded)
        {
            lock (_headerLock)
            {
                if (!_isLocalHeaderLoaded)
                {
                    LoadLocalHeader();
                    _isLocalHeaderLoaded = true;
                }
            }
        }
        return base.GetCompressedStream();
    }

    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_isLocalHeaderLoaded)
        {
            await _asyncHeaderSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!_isLocalHeaderLoaded)
                {
                    await LoadLocalHeaderAsync(cancellationToken);
                    _isLocalHeaderLoaded = true;
                }
            }
            finally
            {
                _asyncHeaderSemaphore.Release();
            }
        }
        return await base.GetCompressedStreamAsync(cancellationToken);
    }

    private void LoadLocalHeader()
    {
        // Use an independent stream for loading the header if possible
        Stream streamToUse = BaseStream;
        bool disposeStream = false;

        if (BaseStream is SourceStream sourceStream && sourceStream.IsFileMode)
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
            Header = _headerFactory.GetLocalHeader(streamToUse, (DirectoryEntryHeader)Header);
        }
        finally
        {
            if (disposeStream)
            {
                streamToUse.Dispose();
            }
        }
    }

    private async ValueTask LoadLocalHeaderAsync(CancellationToken cancellationToken = default)
    {
        // Use an independent stream for loading the header if possible
        Stream streamToUse = BaseStream;
        bool disposeStream = false;

        if (BaseStream is SourceStream sourceStream && sourceStream.IsFileMode)
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
            );
        }
        finally
        {
            if (disposeStream)
            {
                streamToUse.Dispose();
            }
        }
    }

    protected override Stream CreateBaseStream()
    {
        // If BaseStream is a SourceStream in file mode, create an independent stream
        // to support concurrent multi-threaded extraction
        if (BaseStream is SourceStream sourceStream && sourceStream.IsFileMode)
        {
            // Create a new independent stream for this entry
            var independentStream = sourceStream.CreateIndependentStream(0);
            if (independentStream is not null)
            {
                independentStream.Position = Header.DataStartPosition.NotNull();
                return independentStream;
            }
        }

        // Check if BaseStream wraps a FileStream (for multi-volume archives)
        Stream? underlyingStream = BaseStream;
        if (BaseStream is IStreamStack streamStack)
        {
            underlyingStream = streamStack.BaseStream();
        }

        if (underlyingStream is FileStream fileStream)
        {
            // Create a new independent stream from the file
            var independentStream = new FileStream(
                fileStream.Name,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            independentStream.Position = Header.DataStartPosition.NotNull();
            return independentStream;
        }

        // Fall back to existing behavior for stream-based sources
        BaseStream.Position = Header.DataStartPosition.NotNull();
        return BaseStream;
    }
}
