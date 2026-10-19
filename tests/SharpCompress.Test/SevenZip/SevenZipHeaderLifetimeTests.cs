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

    private sealed class TrackingPool : ArrayPool<byte>
    {
        public int Rents { get; private set; }
        public int Returns { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            Rents++;
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
