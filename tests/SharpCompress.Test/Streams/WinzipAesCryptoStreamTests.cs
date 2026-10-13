using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class WinzipAesCryptoStreamTests
{
    public static IEnumerable<object[]> AuthenticationCancellationCases()
    {
        foreach (var length in new[] { 0, 33 })
        {
            foreach (var tagBytesRead in new[] { 0, 4 })
            {
                foreach (var corruption in new[] { "none", "tag", "missing-tag" })
                {
                    foreach (var completion in new[] { "dispose", "retry", "dispose-async" })
                    {
#if LEGACY_DOTNET
                        if (completion == "dispose-async")
                        {
                            continue;
                        }
#endif
                        yield return [length, tagBytesRead, corruption, completion];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AuthenticationCancellationCases))]
    public async Task Authentication_Resumes_After_Cancelled_Tag_Read(
        int length,
        int tagBytesRead,
        string corruption,
        string completion
    )
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        var data = CreateEncryptionData("password", salt);
        var ciphertext = EncryptCtr(new byte[length], data.KeyBytes);
#pragma warning disable CA5350 // WinZip AES mandates HMAC-SHA1-80.
        using var authentication = new HMACSHA1(data.IvBytes);
#pragma warning restore CA5350
        var bytes = ciphertext.Concat(authentication.ComputeHash(ciphertext).Take(10)).ToArray();
        if (corruption == "tag")
        {
            // Corrupt the prefix, which must survive cancellation and be verified on retry.
            bytes[length] ^= 1;
        }
        else if (corruption == "missing-tag")
        {
            bytes = bytes.Take(bytes.Length - 1).ToArray();
        }
        using var cancellation = new CancellationTokenSource();
        using var input = new CancelTagReadStream(bytes, length + tagBytesRead, cancellation);
        using var stream = new WinzipAesCryptoStream(input, data, length);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await stream.ReadAsync(
                new byte[Math.Max(1, length)],
                0,
                Math.Max(1, length),
                cancellation.Token
            )
        );
        Assert.Equal(length + tagBytesRead, input.Position);

        var error = await Record.ExceptionAsync(async () =>
        {
            if (completion == "dispose")
            {
                stream.Dispose();
            }
#if STREAM_DISPOSEASYNC
            else if (completion == "dispose-async")
            {
                await stream.DisposeAsync();
            }
#endif
            else
            {
                Assert.Equal(0, await stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None));
            }
        });
        if (corruption == "none")
        {
            Assert.Null(error);
        }
        else
        {
            Assert.Equal(
                corruption == "tag"
                    ? typeof(SharpCompress.Common.InvalidFormatException)
                    : typeof(SharpCompress.Common.IncompleteArchiveException),
                error?.GetType()
            );
        }
    }

    private sealed class CancelTagReadStream(
        byte[] bytes,
        int cancelPosition,
        CancellationTokenSource cancellation
    ) : MemoryStream(bytes)
    {
        private bool cancelled;

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            if (!cancelled)
            {
                if (Position == cancelPosition)
                {
                    cancelled = true;
                    cancellation.Cancel();
                    return Task.FromCanceled<int>(cancellation.Token);
                }
                count = Math.Min(count, (int)(cancelPosition - Position));
            }
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }

    [Fact]
    public void Dispose_Verifies_Unread_Ciphertext()
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        var stream = CreateStream(
            new byte[33],
            "password",
            salt,
            fragmented: true,
            corruption: "ciphertext"
        );
        Assert.Equal(1, stream.Read(new byte[1], 0, 1));
        Assert.Throws<SharpCompress.Common.InvalidFormatException>(() => stream.Dispose());
        stream.Dispose();
    }

#if STREAM_DISPOSEASYNC
    [Fact]
    public async Task DisposeAsync_Verifies_Unread_Ciphertext()
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        var stream = CreateStream(
            new byte[33],
            "password",
            salt,
            fragmented: true,
            corruption: "ciphertext"
        );
        Assert.Equal(1, await stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None));
        await Assert.ThrowsAsync<SharpCompress.Common.InvalidFormatException>(async () =>
            await stream.DisposeAsync()
        );
        await stream.DisposeAsync();
    }
#endif

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 33)]
    [InlineData(true, 33)]
    public async Task Authentication_Allows_Fragmented_Reads_And_Empty_Payloads(
        bool async,
        int length
    )
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        byte[] payload = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        using var stream = CreateStream(payload, "password", salt, fragmented: true);
        Assert.Equal(0, stream.Read(Array.Empty<byte>(), 0, 0));
        using var output = new MemoryStream();
        if (async)
        {
            await stream.CopyToAsync(output, 81920, CancellationToken.None);
        }
        else
        {
            stream.CopyTo(output);
        }
        Assert.Equal(payload, output.ToArray());
    }

    [Theory]
    [InlineData(false, "ciphertext")]
    [InlineData(true, "ciphertext")]
    [InlineData(false, "missing-tag")]
    [InlineData(true, "missing-tag")]
    public async Task Authentication_Rejects_Tampering_And_Truncated_Tags(
        bool async,
        string corruption
    )
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        using var stream = CreateStream(
            new byte[33],
            "password",
            salt,
            fragmented: true,
            corruption: corruption
        );
        using var output = new MemoryStream();
        if (async)
        {
            var error = await Record.ExceptionAsync(async () =>
                await stream.CopyToAsync(output, 81920, CancellationToken.None)
            );
            Assert.Equal(
                corruption == "ciphertext"
                    ? typeof(SharpCompress.Common.InvalidFormatException)
                    : typeof(SharpCompress.Common.IncompleteArchiveException),
                error!.GetType()
            );
            Assert.Same(
                error,
                await Record.ExceptionAsync(async () =>
                    await stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None)
                )
            );
        }
        else
        {
            var error = Record.Exception(() => stream.CopyTo(output));
            Assert.Equal(
                corruption == "ciphertext"
                    ? typeof(SharpCompress.Common.InvalidFormatException)
                    : typeof(SharpCompress.Common.IncompleteArchiveException),
                error!.GetType()
            );
            Assert.Same(error, Record.Exception(() => stream.Read(new byte[1], 0, 1)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_Rejects_Modified_Authentication_Tag(bool async)
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        var data = CreateEncryptionData("password", salt);
        var cipher = EncryptCtr(new byte[32], data.KeyBytes);
        using var input = new MemoryStream(cipher.Concat(new byte[10]).ToArray());
        using var stream = new WinzipAesCryptoStream(input, data, cipher.Length);
        if (async)
        {
            await Assert.ThrowsAsync<SharpCompress.Common.InvalidFormatException>(async () =>
                await stream.ReadAsync(new byte[32], 0, 32)
            );
        }
        else
        {
            Assert.Throws<SharpCompress.Common.InvalidFormatException>(() =>
                stream.Read(new byte[32], 0, 32)
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_Rejects_Truncated_Ciphertext(bool async)
    {
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        var data = CreateEncryptionData("password", salt);
        using var input = new MemoryStream();
        using var stream = new WinzipAesCryptoStream(input, data, 32);
        if (async)
        {
            await Assert.ThrowsAsync<SharpCompress.Common.IncompleteArchiveException>(async () =>
                await stream.ReadAsync(new byte[32], 0, 32)
            );
        }
        else
        {
            Assert.Throws<SharpCompress.Common.IncompleteArchiveException>(() =>
                stream.Read(new byte[32], 0, 32)
            );
        }
    }

    [Fact]
    public void Read_Decrypts_Data_For_Aligned_Buffer_Size()
    {
        const string password = "sample-password";
        byte[] plainText = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        using var stream = CreateStream(plainText, password, salt);

        byte[] actual = new byte[plainText.Length];
        int bytesRead = stream.Read(actual, 0, actual.Length);

        Assert.Equal(plainText.Length, bytesRead);
        Assert.Equal(plainText, actual);
    }

    [Fact]
    public void Read_Preserves_Keystream_Between_NonAligned_Reads()
    {
        const string password = "sample-password";
        byte[] plainText = Enumerable.Range(0, 97).Select(i => (byte)i).ToArray();
        byte[] salt = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        using var stream = CreateStream(plainText, password, salt);

        byte[] actual = ReadWithChunkPattern(
            (buffer, offset, count) => stream.Read(buffer, offset, count),
            plainText.Length,
            [13, 5, 29, 7, 43]
        );

        Assert.Equal(plainText, actual);
    }

    [Fact]
    public async Task ReadAsync_Preserves_Keystream_Between_NonAligned_Reads()
    {
        const string password = "sample-password";
        byte[] plainText = Enumerable
            .Range(0, 113)
            .Select(i => unchecked((byte)(255 - i)))
            .ToArray();
        byte[] salt = [0x91, 0x82, 0x73, 0x64, 0x55, 0x46, 0x37, 0x28];
        using var stream = CreateStream(plainText, password, salt);

        byte[] actual = await ReadWithChunkPatternAsync(
            (buffer, offset, count) => stream.ReadAsync(buffer, offset, count),
            plainText.Length,
            [11, 3, 17, 5, 41]
        );

        Assert.Equal(plainText, actual);
    }

    [Fact]
    public async Task ReadAsync_Memory_Preserves_Keystream_Between_NonAligned_Reads()
    {
        const string password = "sample-password";
        byte[] plainText = Enumerable
            .Range(0, 113)
            .Select(i => unchecked((byte)(255 - i)))
            .ToArray();
        byte[] salt = [0x91, 0x82, 0x73, 0x64, 0x55, 0x46, 0x37, 0x28];
        using var stream = CreateStream(plainText, password, salt);

        byte[] actual = await ReadWithChunkPatternMemoryAsync(
            stream,
            plainText.Length,
            [11, 3, 17, 5, 41]
        );

        Assert.Equal(plainText, actual);
    }

    [Fact]
    public void Read_Stops_At_Encrypted_Payload_Length()
    {
        const string password = "sample-password";
        byte[] plainText = Enumerable.Range(0, 31).Select(i => (byte)(i * 3)).ToArray();
        byte[] salt = [0xA1, 0xB2, 0xC3, 0xD4, 0x01, 0x12, 0x23, 0x34];
        using var stream = CreateStream(plainText, password, salt);

        byte[] actual = new byte[plainText.Length + 16];
        int bytesRead = stream.Read(actual, 0, actual.Length);
        int eofRead = stream.Read(actual, bytesRead, actual.Length - bytesRead);

        Assert.Equal(plainText.Length, bytesRead);
        Assert.Equal(0, eofRead);
        Assert.Equal(plainText, actual.Take(bytesRead).ToArray());
    }

    private static WinzipAesCryptoStream CreateStream(
        byte[] plainText,
        string password,
        byte[] salt,
        bool fragmented = false,
        string? corruption = null
    )
    {
        var encryptionData = CreateEncryptionData(password, salt);
        byte[] cipherText = EncryptCtr(plainText, encryptionData.KeyBytes);
#pragma warning disable CA5350 // WinZip AES mandates HMAC-SHA1-80.
        using var authentication = new HMACSHA1(encryptionData.IvBytes);
#pragma warning restore CA5350
        byte[] archiveBytes = cipherText
            .Concat(authentication.ComputeHash(cipherText).Take(10))
            .ToArray();
        if (corruption == "ciphertext")
        {
            archiveBytes[0] ^= 1;
        }
        else if (corruption == "missing-tag")
        {
            archiveBytes = archiveBytes.Take(archiveBytes.Length - 1).ToArray();
        }
        Stream input = new MemoryStream(archiveBytes, writable: false);
        if (fragmented)
        {
            input = new ShortReadStream(input);
        }
        return new WinzipAesCryptoStream(input, encryptionData, cipherText.Length);
    }

    [SuppressMessage(
        "Security",
        "CA5379:Rfc2898DeriveBytes might be using a weak hash algorithm",
        Justification = "WinZip AES interop requires PBKDF2 with SHA-1."
    )]
    private static WinzipAesEncryptionData CreateEncryptionData(string password, byte[] salt)
    {
#pragma warning disable SYSLIB0060 // Rfc2898DeriveBytes might be using a weak hash algorithm
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            1000,
            HashAlgorithmName.SHA1
        );
#pragma warning restore SYSLIB0060
        deriveBytes.GetBytes(16);
        deriveBytes.GetBytes(16);
        byte[] passwordVerifyValue = deriveBytes.GetBytes(2);

        return new WinzipAesEncryptionData(
            WinzipAesKeySize.KeySize128,
            salt,
            passwordVerifyValue,
            password
        );
    }

    private static byte[] EncryptCtr(byte[] plainText, byte[] keyBytes)
    {
        using var aes = Aes.Create();
        aes.BlockSize = 128;
        aes.KeySize = keyBytes.Length * 8;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var encryptor = aes.CreateEncryptor(keyBytes, new byte[16]);
        byte[] counter = new byte[16];
        byte[] counterOut = new byte[16];
        byte[] cipherText = new byte[plainText.Length];
        int nonce = 1;
        int offset = 0;

        while (offset < plainText.Length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(counter, nonce++);
            encryptor.TransformBlock(counter, 0, counter.Length, counterOut, 0);

            int blockLength = Math.Min(counterOut.Length, plainText.Length - offset);
            for (int i = 0; i < blockLength; i++)
            {
                cipherText[offset + i] = (byte)(plainText[offset + i] ^ counterOut[i]);
            }

            offset += blockLength;
        }

        return cipherText;
    }

    private static byte[] ReadWithChunkPattern(
        Func<byte[], int, int, int> read,
        int totalLength,
        int[] chunkPattern
    )
    {
        byte[] actual = new byte[totalLength];
        int offset = 0;
        int chunkIndex = 0;

        while (offset < totalLength)
        {
            int requested = Math.Min(
                chunkPattern[chunkIndex % chunkPattern.Length],
                totalLength - offset
            );
            int bytesRead = read(actual, offset, requested);
            Assert.True(bytesRead > 0);
            offset += bytesRead;
            chunkIndex++;
        }

        return actual;
    }

    private static async Task<byte[]> ReadWithChunkPatternAsync(
        Func<byte[], int, int, Task<int>> readAsync,
        int totalLength,
        int[] chunkPattern
    )
    {
        byte[] actual = new byte[totalLength];
        int offset = 0;
        int chunkIndex = 0;

        while (offset < totalLength)
        {
            int requested = Math.Min(
                chunkPattern[chunkIndex % chunkPattern.Length],
                totalLength - offset
            );
            int bytesRead = await readAsync(actual, offset, requested);
            Assert.True(bytesRead > 0);
            offset += bytesRead;
            chunkIndex++;
        }

        return actual;
    }

    private static async Task<byte[]> ReadWithChunkPatternMemoryAsync(
        Stream stream,
        int totalLength,
        int[] chunkPattern
    )
    {
        byte[] actual = new byte[totalLength];
        int offset = 0;
        int chunkIndex = 0;

        while (offset < totalLength)
        {
            int requested = Math.Min(
                chunkPattern[chunkIndex % chunkPattern.Length],
                totalLength - offset
            );
#if NET48
            int bytesRead = await stream.ReadAsync(actual, offset, requested);
#else
            int bytesRead = await stream.ReadAsync(actual.AsMemory(offset, requested));
#endif
            Assert.True(bytesRead > 0);
            offset += bytesRead;
            chunkIndex++;
        }

        return actual;
    }
}
