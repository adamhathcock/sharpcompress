using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;
using Xunit;

namespace SharpCompress.Test.Streams;

public class WinzipAesCryptoStreamTests
{
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
        byte[] salt
    )
    {
        var encryptionData = CreateEncryptionData(password, salt);
        byte[] cipherText = EncryptCtr(plainText, encryptionData.KeyBytes);
        byte[] archiveBytes = cipherText.Concat(new byte[10]).ToArray();
        return new WinzipAesCryptoStream(
            new MemoryStream(archiveBytes, writable: false),
            encryptionData,
            cipherText.Length
        );
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
}
