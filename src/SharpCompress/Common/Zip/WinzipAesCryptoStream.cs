using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal partial class WinzipAesCryptoStream : AsyncDisposableStream
{
    private const int BLOCK_SIZE_IN_BYTES = 16;

    // Counter blocks are encrypted in batches: one native call per 4 KB instead of per 16 bytes.
    private const int KEYSTREAM_BLOCKS = 256;
    private readonly Aes _cipher;
    private readonly byte[] _counterBlocks = new byte[BLOCK_SIZE_IN_BYTES * KEYSTREAM_BLOCKS];
    private readonly byte[] _keystream = new byte[BLOCK_SIZE_IN_BYTES * KEYSTREAM_BLOCKS];
    private int _keystreamOffset;
    private int _keystreamLength;
    private readonly Stream _stream;
    private readonly ICryptoTransform _transform;
    private readonly HMACSHA1 _authentication;
    private bool _authRead;

    // Retain tag progress across canceled reads, including a subsequent synchronous disposal.
    private readonly byte[] _authenticationTag = new byte[10];
    private int _authenticationTagBytesRead;
    private ExceptionDispatchInfo? _authenticationFailure;
    private int _nonce = 1;
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

    private void FillKeystream(int bytesNeeded)
    {
        // Only as many blocks as the read needs; a partial last block carries over to the next read.
        var blocks = Math.Min(
            KEYSTREAM_BLOCKS,
            (bytesNeeded + BLOCK_SIZE_IN_BYTES - 1) / BLOCK_SIZE_IN_BYTES
        );
        for (var i = 0; i < blocks; i++)
        {
            // WinZip AES counter: little-endian block number in the first bytes, the rest stays zero
            BinaryPrimitives.WriteInt32LittleEndian(
                _counterBlocks.AsSpan(i * BLOCK_SIZE_IN_BYTES),
                _nonce++
            );
        }

        var length = blocks * BLOCK_SIZE_IN_BYTES;
        _transform.TransformBlock(_counterBlocks, 0, length, _keystream, 0);
        _keystreamOffset = 0;
        _keystreamLength = length;
    }

    private static void XorInPlace(Span<byte> data, ReadOnlySpan<byte> keystream)
    {
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var dataVectors = MemoryMarshal.Cast<byte, Vector<byte>>(data);
            var keystreamVectors = MemoryMarshal.Cast<byte, Vector<byte>>(keystream);
            for (var v = 0; v < dataVectors.Length; v++)
            {
                dataVectors[v] ^= keystreamVectors[v];
            }
            i = dataVectors.Length * Vector<byte>.Count;
        }

        for (; i < data.Length; i++)
        {
            data[i] ^= keystream[i];
        }
    }

    private void ReadTransformBlocks(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            if (_keystreamOffset == _keystreamLength)
            {
                FillKeystream(count);
            }

            var bytesToXor = Math.Min(count, _keystreamLength - _keystreamOffset);
            XorInPlace(
                buffer.AsSpan(offset, bytesToXor),
                _keystream.AsSpan(_keystreamOffset, bytesToXor)
            );
            _keystreamOffset += bytesToXor;
            offset += bytesToXor;
            count -= bytesToXor;
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
