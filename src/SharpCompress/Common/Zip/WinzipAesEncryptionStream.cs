using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

/// <summary>
/// Stream that encrypts data using WinZip AES encryption and writes to an underlying stream.
/// </summary>
internal class WinzipAesEncryptionStream : Stream
{
    private const int BLOCK_SIZE_IN_BYTES = 16;
    private const int RFC2898_ITERATIONS = 1000;
    private const int AUTH_CODE_LENGTH = 10;

    private readonly Stream _stream;
    private readonly SymmetricAlgorithm _cipher;
    private readonly ICryptoTransform _transform;
    private readonly HMACSHA1 _hmac;
    private readonly byte[] _counter = new byte[BLOCK_SIZE_IN_BYTES];
    private readonly byte[] _counterOut = new byte[BLOCK_SIZE_IN_BYTES];
    private readonly WinzipAesKeySize _keySize;
    private int _nonce = 1;
    private bool _isDisposed;

    internal WinzipAesEncryptionStream(Stream stream, string password, WinzipAesKeySize keySize)
    {
        _stream = stream;
        _keySize = keySize;

        // Generate salt
        var saltLength = GetSaltLength(keySize);
        var salt = new byte[saltLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive keys using PBKDF2
        var keyLength = GetKeyLength(keySize);
        var derivedKeySize = (keyLength * 2) + 2;

#if NET10_0_OR_GREATER
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes,
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1,
            derivedKeySize
        );
        var keyBytes = derivedKey.AsSpan(0, keyLength).ToArray();
        var ivBytes = derivedKey.AsSpan(keyLength, keyLength).ToArray();
        var passwordVerifyValue = derivedKey.AsSpan(keyLength * 2, 2).ToArray();
#elif NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        var rfc2898 = new Rfc2898DeriveBytes(
            password,
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1
        );
        var keyBytes = rfc2898.GetBytes(keyLength);
        var ivBytes = rfc2898.GetBytes(keyLength);
        var passwordVerifyValue = rfc2898.GetBytes(2);
#else
        // .NET Framework and .NET Standard 2.0 only support the 3-parameter constructor
        var rfc2898 = new Rfc2898DeriveBytes(password, salt, RFC2898_ITERATIONS);
        var keyBytes = rfc2898.GetBytes(keyLength);
        var ivBytes = rfc2898.GetBytes(keyLength);
        var passwordVerifyValue = rfc2898.GetBytes(2);
#endif

        // Initialize cipher
        _cipher = CreateCipher(keyBytes);
        var iv = new byte[BLOCK_SIZE_IN_BYTES];
        _transform = _cipher.CreateEncryptor(keyBytes, iv);

        // Initialize HMAC for authentication
        _hmac = new HMACSHA1(ivBytes);

        // Write salt and password verification value
        _stream.Write(salt, 0, salt.Length);
        _stream.Write(passwordVerifyValue, 0, passwordVerifyValue.Length);
    }

    private static int GetSaltLength(WinzipAesKeySize keySize) =>
        keySize switch
        {
            WinzipAesKeySize.KeySize128 => 8,
            WinzipAesKeySize.KeySize192 => 12,
            WinzipAesKeySize.KeySize256 => 16,
            _ => throw new InvalidOperationException(),
        };

    private static int GetKeyLength(WinzipAesKeySize keySize) =>
        keySize switch
        {
            WinzipAesKeySize.KeySize128 => 16,
            WinzipAesKeySize.KeySize192 => 24,
            WinzipAesKeySize.KeySize256 => 32,
            _ => throw new InvalidOperationException(),
        };

    private static SymmetricAlgorithm CreateCipher(byte[] keyBytes)
    {
        var cipher = Aes.Create();
        cipher.BlockSize = BLOCK_SIZE_IN_BYTES * 8;
        cipher.KeySize = keyBytes.Length * 8;
        cipher.Mode = CipherMode.ECB;
        cipher.Padding = PaddingMode.None;
        return cipher;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _stream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return;
        }

        var encrypted = EncryptData(buffer, offset, count);
        _hmac.TransformBlock(encrypted, 0, encrypted.Length, encrypted, 0);
        _stream.Write(encrypted, 0, encrypted.Length);
    }

    private byte[] EncryptData(byte[] buffer, int offset, int count)
    {
        var result = new byte[count];
        var posn = 0;

        while (posn < count)
        {
            var blockSize = Math.Min(BLOCK_SIZE_IN_BYTES, count - posn);

            // Update counter
            BinaryPrimitives.WriteInt32LittleEndian(_counter, _nonce++);

            // Encrypt counter to get key stream
            _transform.TransformBlock(_counter, 0, BLOCK_SIZE_IN_BYTES, _counterOut, 0);

            // XOR with plaintext
            for (var i = 0; i < blockSize; i++)
            {
                result[posn + i] = (byte)(_counterOut[i] ^ buffer[offset + posn + i]);
            }

            posn += blockSize;
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        if (disposing)
        {
            // Finalize HMAC and write authentication code
            _hmac.TransformFinalBlock([], 0, 0);
            var authCode = _hmac.Hash!;
            _stream.Write(authCode, 0, AUTH_CODE_LENGTH);

            _transform.Dispose();
            _cipher.Dispose();
            _hmac.Dispose();
            _stream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Gets the overhead bytes added by encryption (salt + password verification + auth code).
    /// </summary>
    internal static int GetEncryptionOverhead(WinzipAesKeySize keySize) =>
        GetSaltLength(keySize) + 2 + AUTH_CODE_LENGTH;
}
