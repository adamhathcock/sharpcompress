using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

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
