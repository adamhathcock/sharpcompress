using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarCryptoWrapperTests
{
    private static readonly byte[] Plaintext = Enumerable
        .Range(0, 32)
        .Select(x => (byte)x)
        .ToArray();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 7)]
    [InlineData(true, 7)]
    [InlineData(false, 17)]
    [InlineData(true, 17)]
    [InlineData(false, 32)]
    [InlineData(true, 32)]
    public async Task Rar5_Crypto_Reads_Fragmented_Blocks_And_Buffered_Plaintext(
        bool useAsync,
        int readSize
    )
    {
        var info = CreateCryptoInfo();
        using var input = new ShortReadStream(new MemoryStream(Encrypt(info)));
        using var asyncOnly = new AsyncOnlyStream(input, disposeStream: false);
        using var crypto = new RarCryptoWrapper(
            useAsync ? asyncOnly : input,
            info.Salt,
            new CryptKey5("test", info)
        );
        var result = new byte[Plaintext.Length];
        var offset = 0;
        while (offset < result.Length)
        {
            var count = Math.Min(readSize, result.Length - offset);
            var read = useAsync
                ? await crypto.ReadAsync(result, offset, count, CancellationToken.None)
                : crypto.Read(result, offset, count);
            Assert.Equal(count, read);
            offset += read;
        }
        Assert.Equal(Plaintext, result);
        Assert.Equal(0, crypto.Read(result, 0, 0));
        Assert.False(input.IsDisposed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    public async Task Rar5_Crypto_Rejects_Truncated_Ciphertext(int length)
    {
        var info = CreateCryptoInfo();
        var data = Encrypt(info).Take(length).ToArray();
        using var syncInput = new ShortReadStream(new MemoryStream(data));
        using var asyncInput = new ShortReadStream(new MemoryStream(data));
        using var syncCrypto = new RarCryptoWrapper(
            syncInput,
            info.Salt,
            new CryptKey5("test", info)
        );
        using var asyncCrypto = new RarCryptoWrapper(
            asyncInput,
            info.Salt,
            new CryptKey5("test", info)
        );
        Assert.Throws<InvalidFormatException>(() => syncCrypto.Read(new byte[32], 0, 32));
        await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
            await asyncCrypto.ReadAsync(new byte[32], 0, 32, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rar5_Crypto_Async_Cancellation()
    {
        var info = CreateCryptoInfo();
        using var input = new MemoryStream(Encrypt(info));
        using var crypto = new RarCryptoWrapper(input, info.Salt, new CryptKey5("test", info));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await crypto.ReadAsync(new byte[32], 0, 32, cancellation.Token)
        );
        Assert.Equal(0, input.Position);
    }

    private static Rar5CryptoInfo CreateCryptoInfo()
    {
        // Use a one-iteration KDF so these stream tests do not spend time deriving keys.
        using var stream = new MemoryStream(new byte[35]);
        using var reader = new MarkingBinaryReader(stream);
        return Rar5CryptoInfo.Create(reader, true);
    }

    private static byte[] Encrypt(Rar5CryptoInfo info)
    {
        using var aes = Aes.Create();
        aes.Key = CryptKey5.GenerateRarPBKDF2Key("test", info.Salt, 1)[0];
        aes.IV = info.InitV;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(Plaintext, 0, Plaintext.Length);
    }
}
