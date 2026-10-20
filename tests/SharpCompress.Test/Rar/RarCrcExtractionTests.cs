using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Compressors.Rar;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarCrcExtractionTests : ArchiveTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
#if !LEGACY_DOTNET
    [InlineData(2)]
#endif
    public async Task Rar_Archive_Blake2_Empty_Read_Does_Not_Finalize_Hash(int readMode)
    {
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Rar5.crc_blake2.rar");
        using var expectedArchive = RarArchive.OpenArchive(path);
        var expectedEntry = expectedArchive.Entries.First(e => !e.IsDirectory);
        using var expectedInput = expectedEntry.OpenEntryStream();
        using var expected = new MemoryStream();
        expectedInput.CopyTo(expected);

        await using var archive = await RarArchive.OpenAsyncArchive(
            path,
            cancellationToken: CancellationToken.None
        );
        var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
        using var input = await entry.OpenEntryStreamAsync(CancellationToken.None);
        var buffer = new byte[4096];
        using var output = new MemoryStream();
        int read;
        do
        {
            // Empty requests before, during and after extraction must not finalize the hash.
            Assert.Equal(0, await Read(input, buffer, 0, readMode));
            read = await Read(input, buffer, buffer.Length, readMode);
            output.Write(buffer, 0, read);
        } while (read != 0);

        Assert.Equal(0, await Read(input, buffer, 0, readMode));
        Assert.Equal(expected.ToArray(), output.ToArray());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
#if !LEGACY_DOTNET
    [InlineData(2, false)]
    [InlineData(2, true)]
#endif
    public async Task Rar5_Entry_Without_Checksum_Extracts(int readMode, bool useReader)
    {
        using var source = CreateChecksumFreeArchive();
        using var output = new MemoryStream();
        var buffer = new byte[32];
        if (useReader && readMode == 0)
        {
            using var reader = RarReader.OpenReader(source, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            using var input = reader.OpenEntryStream();
            await Drain(input);
        }
        else if (useReader)
        {
            await using var reader = await ReaderFactory.OpenAsyncReader(
                source,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            await using var input = await reader.OpenEntryStreamAsync(CancellationToken.None);
            await Drain(input);
        }
        else
        {
            await using var archive = await RarArchive.OpenAsyncArchive(
                source,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            var entry = (RarArchiveEntry)await archive.EntriesAsync.FirstAsync();
            Assert.Null(entry.FileHeader.FileCrc);
            using var input = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await Drain(input);
        }
        Assert.Equal("hello", Encoding.UTF8.GetString(output.ToArray()));

        async Task Drain(Stream input)
        {
            Assert.Equal(0, await Read(input, buffer, 0, readMode));
            int read;
            while ((read = await Read(input, buffer, buffer.Length, readMode)) != 0)
            {
                output.Write(buffer, 0, read);
            }
        }
    }

    private static async Task<int> Read(Stream input, byte[] buffer, int count, int readMode)
    {
        if (readMode == 0)
        {
            return input.Read(buffer, 0, count);
        }
#if !LEGACY_DOTNET
        if (readMode == 2)
        {
            return await input.ReadAsync(buffer.AsMemory(0, count), CancellationToken.None);
        }
#endif
        return await input.ReadAsync(buffer, 0, count, CancellationToken.None);
    }

    private static MemoryStream CreateChecksumFreeArchive()
    {
        var archive = new MemoryStream();
        using var writer = new BinaryWriter(archive, Encoding.UTF8, leaveOpen: true);
        writer.Write(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x01, 0x00 });
        WriteHeader([1, 0, 0]);
        // Stored RAR5 file, no CRC flag or hash extra record; all vints fit in one byte.
        WriteHeader([2, 2, 5, 0, 5, 0, 0, 1, 1, (byte)'a']);
        writer.Write(Encoding.UTF8.GetBytes("hello"));
        WriteHeader([5, 0, 0]);
        archive.Position = 0;
        return archive;

        void WriteHeader(byte[] body)
        {
            var data = new byte[body.Length + 1];
            data[0] = (byte)body.Length;
            body.CopyTo(data, 1);
            writer.Write(~RarCRC.CheckCrc(uint.MaxValue, data, 0, data.Length));
            writer.Write(data);
        }
    }

    [Theory]
    [InlineData("Rar.rar")]
    [InlineData("Rar5.rar")]
    public void Rar_Archive_WriteToFile_Throws_On_Crc_Mismatch(string archiveName)
    {
        using var archive = RarArchive.OpenArchive(Path.Combine(TEST_ARCHIVES_PATH, archiveName));
        var entry = CorruptFirstFileCrc(archive);
        var destination = Path.Combine(SCRATCH_FILES_PATH, $"{archiveName}-crc-mismatch.txt");

        Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));
    }

    [Theory]
    [InlineData("Rar.rar", false)]
    [InlineData("Rar5.rar", false)]
    [InlineData("Rar5.crc_blake2.rar", false)]
#if !LEGACY_DOTNET
    [InlineData("Rar.rar", true)]
    [InlineData("Rar5.rar", true)]
    [InlineData("Rar5.crc_blake2.rar", true)]
#endif
    public async Task Rar_Archive_ReadAsync_Throws_On_Crc_Mismatch(
        string archiveName,
        bool useMemory
    )
    {
        await using var archive = await RarArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            cancellationToken: CancellationToken.None
        );
        var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
        CorruptCrc((RarArchiveEntry)entry);
        using var input = await entry.OpenEntryStreamAsync(CancellationToken.None);
        var buffer = new byte[4096];

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
#if !LEGACY_DOTNET
            if (useMemory)
            {
                while (await input.ReadAsync(buffer.AsMemory(), CancellationToken.None) != 0) { }
                return;
            }
#endif
            while (await input.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None) != 0) { }
        });
    }

    [Theory]
    [InlineData("Rar.rar")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar5.crc_blake2.rar")]
    public async Task Rar_Archive_WriteToAsync_Throws_On_Crc_Mismatch(string archiveName)
    {
        await using var archive = await RarArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            cancellationToken: CancellationToken.None
        );
        var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
        CorruptCrc((RarArchiveEntry)entry);

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await entry.WriteToAsync(Stream.Null, cancellationToken: CancellationToken.None)
        );
    }

    private static RarArchiveEntry CorruptFirstFileCrc(IArchive archive)
    {
        var entry = archive.Entries.OfType<RarArchiveEntry>().First(e => !e.IsDirectory);
        CorruptCrc(entry);
        return entry;
    }

    private static void CorruptCrc(RarArchiveEntry entry)
    {
        var crc = entry.FileHeader.FileCrc.NotNull();
        crc[0] ^= 0xFF;
    }
}
