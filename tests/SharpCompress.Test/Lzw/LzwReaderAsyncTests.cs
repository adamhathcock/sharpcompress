using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Lzw;
using Xunit;

namespace SharpCompress.Test.Lzw;

public class LzwReaderAsyncTests : ReaderTests
{
    public LzwReaderAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async System.Threading.Tasks.Task Lzw_Reader_Async()
    {
        await ReadAsync("Tar.tar.Z", CompressionType.Lzw);
    }

    [Fact]
    public async System.Threading.Tasks.Task Lzw_Reader_Plain_Z_File_Async()
    {
        // Test async reading of a plain .Z file (not tar-wrapped) using LzwReader directly
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "large_test.txt.Z"));
        using var reader = LzwReader.OpenReader(stream);

        Assert.Equal(ArchiveType.Lzw, reader.ArchiveType);
        Assert.True(reader.MoveToNextEntry());

        var entry = reader.Entry;
        Assert.NotNull(entry);
        Assert.Equal(CompressionType.Lzw, entry.CompressionType);

        // When opened as FileStream, key should be derived from filename
        Assert.Equal("large_test.txt", entry.Key);

        // Decompress asynchronously
        using var entryStream = reader.OpenEntryStream();
        using var ms = new MemoryStream();
        await entryStream.CopyToAsync(ms);

        Assert.Equal(22300, ms.Length);
    }
}
