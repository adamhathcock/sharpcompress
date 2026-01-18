using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Archives.Rar;

internal class SeekableFilePart : RarFilePart
{
    private readonly Stream _stream;
    private readonly string? _password;

    internal SeekableFilePart(
        MarkHeader mh,
        FileHeader fh,
        int index,
        Stream stream,
        string? password
    )
        : base(mh, fh, index)
    {
        _stream = stream;
        _password = password;
    }

    internal override Stream GetCompressedStream()
    {
        Stream streamToUse;

        // If the stream is a SourceStream in file mode, create an independent stream
        // to support concurrent multi-threaded extraction
        if (_stream is SourceStream sourceStream && sourceStream.IsFileMode)
        {
            var independentStream = sourceStream.CreateIndependentStream(0);
            if (independentStream is not null)
            {
                streamToUse = independentStream;
                streamToUse.Position = FileHeader.DataStartPosition;

                if (FileHeader.R4Salt != null)
                {
                    var cryptKey = new CryptKey3(_password!);
                    return new RarCryptoWrapper(streamToUse, FileHeader.R4Salt, cryptKey);
                }

                if (FileHeader.Rar5CryptoInfo != null)
                {
                    var cryptKey = new CryptKey5(_password!, FileHeader.Rar5CryptoInfo);
                    return new RarCryptoWrapper(
                        streamToUse,
                        FileHeader.Rar5CryptoInfo.Salt,
                        cryptKey
                    );
                }

                return streamToUse;
            }
        }

        // Check if the stream wraps a FileStream
        Stream? underlyingStream = _stream;
        if (_stream is IStreamStack streamStack)
        {
            underlyingStream = streamStack.BaseStream();
        }

        if (underlyingStream is FileStream fileStream)
        {
            // Create a new independent stream from the file
            streamToUse = new FileStream(
                fileStream.Name,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            streamToUse.Position = FileHeader.DataStartPosition;

            if (FileHeader.R4Salt != null)
            {
                var cryptKey = new CryptKey3(_password!);
                return new RarCryptoWrapper(streamToUse, FileHeader.R4Salt, cryptKey);
            }

            if (FileHeader.Rar5CryptoInfo != null)
            {
                var cryptKey = new CryptKey5(_password!, FileHeader.Rar5CryptoInfo);
                return new RarCryptoWrapper(streamToUse, FileHeader.Rar5CryptoInfo.Salt, cryptKey);
            }

            return streamToUse;
        }

        // Fall back to existing behavior for stream-based sources
        _stream.Position = FileHeader.DataStartPosition;

        if (FileHeader.R4Salt != null)
        {
            var cryptKey = new CryptKey3(_password!);
            return new RarCryptoWrapper(_stream, FileHeader.R4Salt, cryptKey);
        }

        if (FileHeader.Rar5CryptoInfo != null)
        {
            var cryptKey = new CryptKey5(_password!, FileHeader.Rar5CryptoInfo);
            return new RarCryptoWrapper(_stream, FileHeader.Rar5CryptoInfo.Salt, cryptKey);
        }

        return _stream;
    }

    internal override string FilePartName => "Unknown Stream - File Entry: " + FileHeader.FileName;
}
