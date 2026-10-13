using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal partial class WinzipAesCryptoStream : AsyncDisposableStream
{
    private const int BLOCK_SIZE_IN_BYTES = 16;
    private readonly Aes _cipher;
    private readonly byte[] _counter = new byte[BLOCK_SIZE_IN_BYTES];
    private readonly Stream _stream;
    private readonly ICryptoTransform _transform;
    private readonly HMACSHA1 _authentication;
    private bool _authRead;

    // Retain tag progress across canceled reads, including a subsequent synchronous disposal.
    private readonly byte[] _authenticationTag = new byte[10];
    private int _authenticationTagBytesRead;
    private ExceptionDispatchInfo? _authenticationFailure;
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
        if (length < 0)
        {
            throw new InvalidFormatException("Invalid WinZip AES payload size.");
        }
        // PBKDF2's second key is the authentication key, not the CTR initialization vector.
#pragma warning disable CA5350 // WinZip AES mandates HMAC-SHA1-80 for interoperability.
        _authentication = new HMACSHA1(winzipAesEncryptionData.IvBytes);
#pragma warning restore CA5350

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
            try
            {
                if (!_authRead)
                {
#if !STREAM_DISPOSEASYNC
                    // Legacy Stream lacks DisposeAsync, including for async-only inputs.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
#pragma warning disable CA2012
                    DrainAndVerifyAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
#else
                    var buffer = new byte[4096];
                    while (_totalBytesLeftToRead > 0)
                    {
                        Read(buffer, 0, buffer.Length);
                    }
                    VerifyAuthentication();
#endif
                }
            }
            finally
            {
                _authentication.Dispose();
                _transform.Dispose();
                _cipher.Dispose();
                _stream.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    private void VerifyAuthentication()
    {
        _authenticationFailure?.Throw();
        if (_authRead)
        {
            return;
        }
        try
        {
            while (_authenticationTagBytesRead < _authenticationTag.Length)
            {
                var read = _stream.Read(
                    _authenticationTag,
                    _authenticationTagBytesRead,
                    _authenticationTag.Length - _authenticationTagBytesRead
                );
                if (read == 0)
                {
                    throw new IncompleteArchiveException(
                        "Unexpected end of WinZip AES authentication tag."
                    );
                }
                _authenticationTagBytesRead += read;
            }
            ValidateAuthentication(_authenticationTag);
            _authRead = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _authRead = true;
            _authenticationFailure = ExceptionDispatchInfo.Capture(exception);
            throw;
        }
    }

    private void ValidateAuthentication(byte[] tag)
    {
        _authentication.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var expected = _authentication.Hash!;
        var difference = 0;
        for (var i = 0; i < tag.Length; i++)
        {
            difference |= tag[i] ^ expected[i];
        }
        if (difference != 0)
        {
            throw new InvalidFormatException("WinZip AES authentication failed.");
        }
    }

    public override void Flush() { }

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
