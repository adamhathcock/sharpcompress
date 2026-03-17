using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip;

internal partial class WinzipAesCryptoStream : Stream
{
    private const int BLOCK_SIZE_IN_BYTES = 16;
    private readonly Aes _cipher;
    private readonly byte[] _counter = new byte[BLOCK_SIZE_IN_BYTES];
    private readonly Stream _stream;
    private readonly ICryptoTransform _transform;
    private int _nonce = 1;
    private byte[] _counterOut = new byte[BLOCK_SIZE_IN_BYTES];
    private int _counterOutOffset = BLOCK_SIZE_IN_BYTES;
    private long _totalBytesLeftToRead;
    private bool _isDisposed;

    internal WinzipAesCryptoStream(
        Stream stream,
        WinzipAesEncryptionData winzipAesEncryptionData,
        long length
    )
    {
        _stream = stream;
        _totalBytesLeftToRead = length;

        _cipher = CreateCipher(winzipAesEncryptionData);

        var iv = new byte[BLOCK_SIZE_IN_BYTES];
        _transform = _cipher.CreateEncryptor(winzipAesEncryptionData.KeyBytes, iv);
    }

    private Aes CreateCipher(WinzipAesEncryptionData winzipAesEncryptionData)
    {
        var cipher = Aes.Create();
        cipher.BlockSize = BLOCK_SIZE_IN_BYTES * 8;
        cipher.KeySize = winzipAesEncryptionData.KeyBytes.Length * 8;
        cipher.Mode = CipherMode.ECB;
        cipher.Padding = PaddingMode.None;
        return cipher;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            base.Dispose(disposing);
            return;
        }
        _isDisposed = true;
        if (disposing)
        {
            // Read out last 10 auth bytes - catch exceptions for async-only streams
            if (Utility.UseSyncOverAsyncDispose())
            {
                var ten = ArrayPool<byte>.Shared.Rent(10);
                try
                {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
#pragma warning disable CA2012
                    _stream.ReadFullyAsync(ten, 0, 10).GetAwaiter().GetResult();
#pragma warning restore CA2012
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(ten);
                }
            }
            else
            {
                Span<byte> ten = stackalloc byte[10];
                _stream.ReadFully(ten);
            }
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }

    private async Task ReadAuthBytesAsync()
    {
        byte[] authBytes = new byte[10];
        await _stream.ReadFullyAsync(authBytes, 0, 10).ConfigureAwait(false);
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_totalBytesLeftToRead == 0)
        {
            return 0;
        }
        var bytesToRead = count;
        if (count > _totalBytesLeftToRead)
        {
            bytesToRead = (int)_totalBytesLeftToRead;
        }
        var read = _stream.Read(buffer, offset, bytesToRead);
        _totalBytesLeftToRead -= read;

        ReadTransformBlocks(buffer, offset, read);

        return read;
    }

    private void FillCounterOut()
    {
        // update the counter
        BinaryPrimitives.WriteInt32LittleEndian(_counter, _nonce++);
        _transform.TransformBlock(
            _counter,
            0, // offset
            BLOCK_SIZE_IN_BYTES,
            _counterOut,
            0
        ); // offset
        _counterOutOffset = 0;
    }

    private void XorInPlace(byte[] buffer, int offset, int count, int counterOffset)
    {
        for (var i = 0; i < count; i++)
        {
            buffer[offset + i] = (byte)(_counterOut[counterOffset + i] ^ buffer[offset + i]);
        }
    }

    private void ReadTransformBlocks(byte[] buffer, int offset, int count)
    {
        var posn = offset;
        var remaining = count;

        while (posn < buffer.Length && remaining > 0)
        {
            if (_counterOutOffset == BLOCK_SIZE_IN_BYTES)
            {
                FillCounterOut();
            }

            var bytesToXor = Math.Min(BLOCK_SIZE_IN_BYTES - _counterOutOffset, remaining);
            XorInPlace(buffer, posn, bytesToXor, _counterOutOffset);
            _counterOutOffset += bytesToXor;
            posn += bytesToXor;
            remaining -= bytesToXor;
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
