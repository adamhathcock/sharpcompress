using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal enum CryptoMode
{
    Encrypt,
    Decrypt,
}

internal class PkwareTraditionalCryptoStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { return; }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { return; }
    }

    void IStreamStack.SetPosition(long position) { }

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

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(PkwareTraditionalCryptoStream));
#endif
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

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_mode == CryptoMode.Encrypt)
        {
            throw new NotSupportedException("This stream does not encrypt via Read()");
        }

        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        var temp = new byte[count];
        var readBytes = _stream.Read(temp, 0, count);
        var decrypted = _encryptor.Decrypt(temp, readBytes);
        Buffer.BlockCopy(decrypted, 0, buffer, offset, readBytes);
        return readBytes;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_mode == CryptoMode.Decrypt)
        {
            throw new NotSupportedException("This stream does not Decrypt via Write()");
        }

        if (count == 0)
        {
            return;
        }

        byte[] plaintext;
        if (offset != 0)
        {
            plaintext = new byte[count];
            Buffer.BlockCopy(buffer, offset, plaintext, 0, count);
        }
        else
        {
            plaintext = buffer;
        }

        var encrypted = _encryptor.Encrypt(plaintext, count);
        _stream.Write(encrypted, 0, encrypted.Length);
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
#if DEBUG_STREAMS
        this.DebugDispose(typeof(PkwareTraditionalCryptoStream));
#endif
        base.Dispose(disposing);
        _stream.Dispose();
    }
}
