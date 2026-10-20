using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Crypto;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarCryptoWrapperTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 15)]
    [InlineData(true, 17)]
    [InlineData(false, 8191)]
    [InlineData(true, 8192)]
    [InlineData(false, 8193)]
    [InlineData(true, 32784)]
    public async Task Rar5_Crypto_Reads_Large_Data_Without_Reading_Next_Entry(
        bool useAsync,
        int readSize
    )
    {
        var plaintext = Enumerable.Range(0, 32784).Select(i => (byte)i).ToArray();
        var info = CreateCryptoInfo();
        var ciphertext = Encrypt(info, plaintext);
        using var input = new MemoryStream(ciphertext.Concat(new byte[16]).ToArray());
        using var crypto = new RarCryptoWrapper(input, info.Salt, new CryptKey5("test", info));
        var result = new byte[plaintext.Length + 2];
        var offset = 0;
        while (offset < plaintext.Length)
        {
            var count = Math.Min(readSize, plaintext.Length - offset);
            var read = useAsync
                ? await crypto.ReadAsync(result, offset + 1, count, CancellationToken.None)
                : crypto.Read(result, offset + 1, count);
            Assert.Equal(count, read);
            offset += read;
            Assert.Equal((offset + 15) & ~15, input.Position);
        }
        Assert.Equal(plaintext, result.Skip(1).Take(plaintext.Length));
        Assert.Equal(0, result[0]);
        Assert.Equal(0, result[result.Length - 1]);
        Assert.Equal(ciphertext.Length, input.Position);
    }

#if !LEGACY_DOTNET
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar5_Crypto_Reads_Span_And_Memory(bool useAsync)
    {
        var info = CreateCryptoInfo();
        using var input = new MemoryStream(Encrypt(info));
        using var crypto = new RarCryptoWrapper(input, info.Salt, new CryptKey5("test", info));
        var output = new byte[32];
        for (var i = 0; i < output.Length; i++)
        {
            var read = useAsync
                ? await crypto.ReadAsync(output.AsMemory(i, 1), CancellationToken.None)
                : crypto.Read(output.AsSpan(i, 1));
            Assert.Equal(1, read);
        }
        Assert.Equal(Plaintext, output);
    }

    [Fact]
    public void Rar5_Crypto_DoesNotAllocate_PerBlock()
    {
        var info = CreateCryptoInfo();
        var plaintext = new byte[1024 * 1024];
        using var input = new MemoryStream(Encrypt(info, plaintext));
        using var crypto = new RarCryptoWrapper(input, info.Salt, new CryptKey5("test", info));
        var buffer = new byte[8192];
        Assert.Equal(buffer.Length, crypto.Read(buffer, 0, buffer.Length));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var i = buffer.Length; i < plaintext.Length; i += buffer.Length)
        {
            total += crypto.Read(buffer, 0, buffer.Length);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(plaintext.Length - buffer.Length, total);
        Assert.True(allocated < 16 * 1024, $"Allocated {allocated} bytes.");
    }
#endif

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar5_Crypto_Header_Reads_Track_Crc_And_Leave_Stream_Open(bool useAsync)
    {
        var info = CreateCryptoInfo();
        using var input = new ShortReadStream(new MemoryStream(Encrypt(info)));
        uint expectedCrc = SharpCompress.Compressors.Rar.RarCRC.CheckCrc(
            0xffffffff,
            Plaintext,
            0,
            Plaintext.Length
        );
        if (useAsync)
        {
            using var asyncOnly = new AsyncOnlyStream(input, disposeStream: false);
            using var reader = await AsyncRarCryptoBinaryReader.Create(
                asyncOnly,
                new CryptKey5("test", info),
                info.Salt
            );
            reader.ResetCrc();
            Assert.Equal(Plaintext[0], await reader.ReadByteAsync());
            Assert.Equal(Plaintext.Skip(1), await reader.ReadBytesAsync(31));
            Assert.Equal(32, reader.CurrentReadByteCount);
            Assert.Equal(~expectedCrc, reader.GetCrc32());
        }
        else
        {
            using var reader = RarCryptoBinaryReader.Create(
                input,
                new CryptKey5("test", info),
                info.Salt
            );
            reader.ResetCrc();
            Assert.Equal(Plaintext[0], reader.ReadByte());
            Assert.Equal(Plaintext.Skip(1), reader.ReadBytes(31));
            Assert.Equal(32, reader.CurrentReadByteCount);
            Assert.Equal(~expectedCrc, reader.GetCrc32());
        }
        Assert.False(input.IsDisposed);
    }

    [Fact]
    public void Rar_Crypto_BlockTransformer_Disposes_Transform()
    {
        var transform = new TrackingTransform();
        using (var block = new BlockTransformer(transform))
        {
            block.ProcessBlocks(new byte[32], 32);
            Assert.False(transform.IsDisposed);
        }
        Assert.True(transform.IsDisposed);
    }

    private sealed class TrackingTransform : ICryptoTransform
    {
        public bool IsDisposed { get; private set; }
        public int InputBlockSize => 16;
        public int OutputBlockSize => 16;
        public bool CanTransformMultipleBlocks => true;
        public bool CanReuseTransform => false;

        public int TransformBlock(
            byte[] inputBuffer,
            int inputOffset,
            int inputCount,
            byte[] outputBuffer,
            int outputOffset
        )
        {
            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
            return inputCount;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) =>
            throw new NotSupportedException();

        public void Dispose() => IsDisposed = true;
    }

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
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
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

    private static byte[] Encrypt(Rar5CryptoInfo info, byte[]? plaintext = null)
    {
        using var aes = Aes.Create();
        aes.Key = CryptKey5.GenerateRarPBKDF2Key("test", info.Salt, 1)[0];
        aes.IV = info.InitV;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        plaintext ??= Plaintext;
        return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }
}
