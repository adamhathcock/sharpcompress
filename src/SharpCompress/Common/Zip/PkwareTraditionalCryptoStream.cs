using System;
using System.IO;

namespace SharpCompress.Common.Zip;

internal enum CryptoMode
{
    Encrypt,
    Decrypt,
}

internal partial class PkwareTraditionalCryptoStream : Stream
{
    private readonly PkwareTraditionalEncryptionData _encryptor;
    private readonly CryptoMode _mode;
    private readonly Stream _stream;
    private bool _isDisposed;

    public PkwareTraditionalCryptoStream(
        Stream stream,
        PkwareTraditionalEncryptionData encryptor,
        CryptoMode mode
    )
    {
        _encryptor = encryptor;
        _stream = stream;
        _mode = mode;
    }

    public override bool CanRead => (_mode == CryptoMode.Decrypt);

    public override bool CanSeek => false;

    public override bool CanWrite => (_mode == CryptoMode.Encrypt);

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        base.Dispose(disposing);
        _stream.Dispose();
    }
}
