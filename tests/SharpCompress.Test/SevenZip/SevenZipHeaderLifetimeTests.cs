using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.Crypto;
using Xunit;

namespace SharpCompress.Test.SevenZip;

[Collection(nameof(SevenZipHeaderAllocationTests))]
public class SevenZipHeaderLifetimeTests : TestBase
{
    [Theory]
    [InlineData("7Zip.LZMA.7z", false)]
    [InlineData("7Zip.LZMA.7z", true)]
    [InlineData("7Zip.LZMA2.7z", false)]
    [InlineData("7Zip.LZMA2.7z", true)]
    [InlineData("7Zip.solid.7z", false)]
    [InlineData("7Zip.solid.7z", true)]
    public async Task EncodedHeader_Releases_Pack_Buffer_Without_Closing_Source(
        string filename,
        bool asyncRead
    )
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        var pool = new TrackingPool();
        var reader = new ArchiveReader(pool);
        ArchiveDatabase database;
        if (asyncRead)
        {
            await reader.OpenAsync(input, false, CancellationToken.None);
            database = await reader.ReadDatabaseAsync(new NoPassword(), CancellationToken.None);
        }
        else
        {
            reader.Open(input, false);
            database = reader.ReadDatabase(new NoPassword());
        }

        Assert.NotEmpty(database._files);
        Assert.True(pool.Rents > 0, "The fixture must contain an encoded header.");
        Assert.Equal(pool.Rents, pool.Returns);
        Assert.True(input.CanRead);
        // The parsed metadata must remain usable after the temporary decoder is disposed.
        Assert.All(database._files, file => Assert.NotNull(file.Name));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncodedHeader_Crc_Failure_Returns_Pack_Buffer(bool asyncRead)
    {
        // A Copy-coded encoded header containing kHeader/kEnd, with an incorrect
        // decoded-header CRC but a valid next-header CRC. This reaches validation
        // after the decoder has been constructed and read successfully.
        byte[] encodedHeader =
        [
            0x17,
            0x06,
            0,
            1,
            0x09,
            2,
            0,
            0x07,
            0x0B,
            1,
            0,
            1,
            1,
            0,
            0x0C,
            2,
            0x0A,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
        ];
        using var input = new MemoryStream();
        SevenZipSignatureHeaderWriter.WriteFinal(
            input,
            2,
            (ulong)encodedHeader.Length,
            Crc32Stream.Compute(encodedHeader)
        );
        input.Write(new byte[] { 1, 0 }, 0, 2);
        input.Write(encodedHeader, 0, encodedHeader.Length);
        input.Position = 0;
        var pool = new TrackingPool();
        var reader = new ArchiveReader(pool);
        if (asyncRead)
        {
            await reader.OpenAsync(input, false, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await reader.ReadDatabaseAsync(new NoPassword(), CancellationToken.None)
            );
        }
        else
        {
            reader.Open(input, false);
            Assert.Throws<InvalidFormatException>(() => reader.ReadDatabase(new NoPassword()));
        }

        Assert.True(pool.Rents > 0);
        Assert.Equal(pool.Rents, pool.Returns);
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public async Task Decoded_Metadata_Is_Not_Copied_And_Returns_All_Buffers(
        bool asyncRead,
        int failure
    )
    {
        using var decoded = new MemoryStream();
        decoded.Write(new byte[] { failure == 1 ? (byte)99 : (byte)1, 2, 26 }, 0, 3);
        decoded.WriteEncodedUInt64(1 << 20);
        var padding = new byte[1 << 20];
        decoded.Write(padding, 0, padding.Length);
        decoded.WriteByte(0);
        decoded.WriteByte(0);
        using var encoded = new MemoryStream();
        encoded.Write(new byte[] { 23, 6, 0, 1, 9 }, 0, 5);
        encoded.WriteEncodedUInt64((ulong)decoded.Length);
        encoded.Write(new byte[] { 0, 7, 11, 1, 0, 1, 1, 0, 12 }, 0, 9);
        encoded.WriteEncodedUInt64((ulong)decoded.Length + (failure == 2 ? 8192ul : 0));
        encoded.WriteByte(0);
        encoded.WriteByte(0);
        var header = encoded.ToArray();
        using var input = new MemoryStream();
        SevenZipSignatureHeaderWriter.WriteFinal(
            input,
            (ulong)decoded.Length,
            (ulong)header.Length,
            Crc32Stream.Compute(header)
        );
        decoded.Position = 0;
        decoded.CopyTo(input);
        input.Write(header, 0, header.Length);
        input.Position = 0;
        using var cancellation = new CancellationTokenSource();
        var pool = new TrackingPool
        {
            OnRent =
                failure == 3
                    ? count =>
                    {
                        // Scratch and packed-input buffers are rented before metadata storage.
                        if (count == 3)
                        {
                            cancellation.Cancel();
                        }
                    }
                    : null,
        };
        var reader = new ArchiveReader(pool);
        if (asyncRead)
        {
            await reader.OpenAsync(input, false, CancellationToken.None);
        }
        else
        {
            reader.Open(input, false);
        }
#if !LEGACY_DOTNET
        var before = GC.GetTotalAllocatedBytes(precise: true);
#endif
        if (failure == 3)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await reader.ReadDatabaseAsync(new NoPassword(), cancellation.Token)
            );
        }
        else if (failure == 0)
        {
            var database = asyncRead
                ? await reader.ReadDatabaseAsync(new NoPassword(), CancellationToken.None)
                : reader.ReadDatabase(new NoPassword());
            Assert.Empty(database._files);
        }
        else if (asyncRead)
        {
            await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
                await reader.ReadDatabaseAsync(new NoPassword(), CancellationToken.None)
            );
        }
        else
        {
            Assert.ThrowsAny<SharpCompressException>(() => reader.ReadDatabase(new NoPassword()));
        }
#if !LEGACY_DOTNET
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Assert.True(
            allocated < pool.AllocatedBytes + 512 * 1024,
            $"Allocated {allocated} bytes including {pool.AllocatedBytes} bytes of pooled storage."
        );
#endif
        Assert.True(pool.Rents > 0);
        Assert.Equal(1, pool.ScratchRents);
        Assert.Equal(pool.Rents, pool.Returns);
        Assert.True(input.CanRead);
    }

    private sealed class TrackingPool : ArrayPool<byte>
    {
        public int Rents { get; private set; }
        public int Returns { get; private set; }
        public int ScratchRents { get; private set; }
        public long AllocatedBytes { get; private set; }
        public Action<int>? OnRent { get; set; }

        public override byte[] Rent(int minimumLength)
        {
            Rents++;
            if (minimumLength == 8192)
            {
                ScratchRents++;
            }
            AllocatedBytes += minimumLength;
            OnRent?.Invoke(Rents);
            return new byte[minimumLength];
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            Returns++;
            array.AsSpan().Fill(0xCC);
        }
    }

    private sealed class NoPassword : IPasswordProvider
    {
        public string? CryptoGetTextPassword() => null;
    }
}
