using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Common.Zip;

internal partial class SeekableZipFilePart : ZipFilePart
{
    private volatile bool _isLocalHeaderLoaded;
    private readonly SeekableZipHeaderFactory _headerFactory;
    private readonly object _headerLock = new();

    internal SeekableZipFilePart(
        SeekableZipHeaderFactory headerFactory,
        DirectoryEntryHeader header,
        Stream stream,
        CompressionProviderRegistry compressionProviders
    )
        : base(header, stream, compressionProviders) => _headerFactory = headerFactory;

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

    private void LoadLocalHeader()
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

    protected override Stream CreateBaseStream()
    {
        // If BaseStream is a SourceStream in file mode with multi-threading enabled,
        // create an independent stream to support concurrent extraction
        if (
            BaseStream is SourceStream sourceStream
            && sourceStream.IsFileMode
            && sourceStream.ReaderOptions.EnableMultiThreadedExtraction
        )
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
