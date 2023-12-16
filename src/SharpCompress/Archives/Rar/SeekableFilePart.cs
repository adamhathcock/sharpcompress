using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Archives.Rar;

internal class SeekableFilePart : RarFilePart
{
    private readonly Stream stream;
    private readonly string? password;

    internal SeekableFilePart(
        MarkHeader mh,
        FileHeader fh,
        int index,
        Stream stream,
        string? password
    )
        : base(mh, fh, index)
    {
        this.stream = stream;
        this.password = password;
    }

    internal override Stream GetCompressedStream()
    {
        stream.Position = FileHeader.DataStartPosition;

        if (FileHeader.R4Salt != null)
        {
            var cryptKey = new CryptKey3(password!);
            return new RarCryptoWrapper(stream, FileHeader.R4Salt, cryptKey);
        }

        if (FileHeader.Rar5CryptoInfo != null)
        {
            var cryptKey = new CryptKey5(password!, FileHeader.Rar5CryptoInfo);
            return new RarCryptoWrapper(stream, FileHeader.Rar5CryptoInfo.Salt, cryptKey);
        }

        return stream;
    }

    internal override string FilePartName => "Unknown Stream - File Entry: " + FileHeader.FileName;
}
