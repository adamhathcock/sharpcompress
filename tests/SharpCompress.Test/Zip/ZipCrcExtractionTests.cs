using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipCrcExtractionTests : ArchiveTests
{
    private const string EntryName = "crc.txt";
    private static readonly byte[] EntryData = Encoding.UTF8.GetBytes("crc validation payload");

    [Fact]
    public void Zip_Archive_WriteToFile_Throws_On_Crc_Mismatch()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-crc-mismatch.txt");

        var exception = Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));

        Assert.Contains(EntryName, exception.Message);
    }

    [Fact]
    public void Zip_Archive_WriteToFile_Skips_Crc_Mismatch_When_Disabled()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-crc-disabled.txt");

        entry.WriteToFile(destination, new ExtractionOptions { CheckCrc = false });

        Assert.Equal(EntryData, File.ReadAllBytes(destination));
    }

    [Fact]
    public void Zip_Archive_WriteTo_Throws_On_Crc_Mismatch_When_Enabled()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        using var destination = new MemoryStream();

        var exception = Assert.Throws<InvalidFormatException>(() =>
            entry.WriteTo(destination, new ExtractionOptions { CheckCrc = true })
        );

        Assert.Contains(EntryName, exception.Message);
    }

    [Fact]
    public void Zip_Archive_WriteTo_Skips_Crc_Mismatch_When_Disabled()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        using var destination = new MemoryStream();

        entry.WriteTo(destination, new ExtractionOptions { CheckCrc = false });

        Assert.Equal(EntryData, destination.ToArray());
    }

    [Fact]
    public void Zip_Reader_WriteEntryToFile_Throws_On_Crc_Mismatch()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var reader = ReaderFactory.OpenReader(zipStream);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-reader-crc-mismatch.txt");

        Assert.True(reader.MoveToNextEntry());
        var exception = Assert.Throws<InvalidFormatException>(() =>
            reader.WriteEntryToFile(destination)
        );

        Assert.Contains(EntryName, exception.Message);
    }

    [Fact]
    public void Zip_Reader_WriteEntryToFile_Skips_Crc_Mismatch_When_Disabled()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var reader = ReaderFactory.OpenReader(zipStream);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-reader-crc-disabled.txt");

        Assert.True(reader.MoveToNextEntry());
        reader.WriteEntryToFile(destination, new ExtractionOptions { CheckCrc = false });

        Assert.Equal(EntryData, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task Zip_Archive_WriteToFileAsync_Throws_On_Crc_Mismatch()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-crc-mismatch-async.txt");

        var exception = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await entry.WriteToFileAsync(destination)
        );

        Assert.Contains(EntryName, exception.Message);
    }

    [Fact]
    public async Task Zip_Archive_WriteToAsync_Throws_On_Crc_Mismatch_When_Enabled()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        // MemoryStream has nothing to dispose asynchronously
        using var destination = new MemoryStream();

        var exception = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await entry.WriteToAsync(destination, new ExtractionOptions { CheckCrc = true })
        );

        Assert.Contains(EntryName, exception.Message);
    }

    [Fact]
    public async Task Zip_Archive_WriteToAsync_Skips_Crc_Mismatch_When_Disabled()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        // MemoryStream has nothing to dispose asynchronously
        using var destination = new MemoryStream();

        await entry.WriteToAsync(destination, new ExtractionOptions { CheckCrc = false });

        Assert.Equal(EntryData, destination.ToArray());
    }

    [Fact]
    public async Task Zip_Reader_WriteEntryToFileAsync_Throws_On_Crc_Mismatch()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: false);
        await using var reader = await ReaderFactory.OpenAsyncReader(zipStream);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-reader-crc-mismatch-async.txt");

        Assert.True(await reader.MoveToNextEntryAsync());
        var exception = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await reader.WriteEntryToFileAsync(destination)
        );

        Assert.Contains(EntryName, exception.Message);
    }

    [Fact]
    public void Zip_Archive_WriteToFile_Throws_On_DataDescriptor_Crc_Mismatch()
    {
        using var zipStream = CreateZipWithInvalidCrc(useDataDescriptor: true);
        using var archive = ZipArchive.OpenArchive(zipStream);
        var entry = archive.Entries.Single(e => !e.IsDirectory);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "zip-dd-crc-mismatch.txt");

        var exception = Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));

        Assert.Contains(EntryName, exception.Message);
    }

    private static MemoryStream CreateZipWithInvalidCrc(bool useDataDescriptor)
    {
        var zipStream = new MemoryStream();
        Stream writerStream = useDataDescriptor ? new NonSeekableWriteStream(zipStream) : zipStream;
        using (
            var writer = WriterFactory.OpenWriter(
                writerStream,
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            )
        )
        {
            writer.Write(EntryName, new MemoryStream(EntryData));
        }

        var bytes = zipStream.ToArray();
        CorruptCrc(bytes, ZipHeaderFactoryEntrySignature, 14);
        CorruptCrc(bytes, ZipHeaderFactoryDirectorySignature, 16);
        return new MemoryStream(bytes);
    }

    private const uint ZipHeaderFactoryEntrySignature = 0x04034b50;
    private const uint ZipHeaderFactoryDirectorySignature = 0x02014b50;

    private static void CorruptCrc(byte[] bytes, uint signature, int crcOffset)
    {
        var offset = FindSignature(bytes, signature);
        var crcIndex = offset + crcOffset;
        bytes[crcIndex] ^= 0xFF;
    }

    private static int FindSignature(byte[] bytes, uint signature)
    {
        var signatureBytes = BitConverter.GetBytes(signature);
        for (var i = 0; i <= bytes.Length - signatureBytes.Length; i++)
        {
            if (bytes.AsSpan(i, signatureBytes.Length).SequenceEqual(signatureBytes))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"ZIP signature 0x{signature:X8} was not found.");
    }

    private sealed class NonSeekableWriteStream(Stream stream) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => stream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            stream.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            stream.Write(buffer, offset, count);

#if !LEGACY_DOTNET
        public override void Write(ReadOnlySpan<byte> buffer) => stream.Write(buffer);
#endif
    }
}
