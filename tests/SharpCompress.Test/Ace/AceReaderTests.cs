using System.IO;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Readers.Ace;
using Xunit;

namespace SharpCompress.Test.Ace;

public class AceReaderTests : ReaderTests
{
    public AceReaderTests()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
    }

    [Fact]
    public void Ace_Stored_Read()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Ace.stored.ace");
        using var stream = File.OpenRead(archivePath);
        using var reader = AceReader.Open(stream, new ReaderOptions());

        int entryCount = 0;
        while (reader.MoveToNextEntry())
        {
            Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
            Assert.False(reader.Entry.IsDirectory);
            Assert.NotNull(reader.Entry.Key);
            entryCount++;
        }
        Assert.True(entryCount > 0, "Expected at least one entry in the archive");
    }

    [Fact]
    public void Ace_Stored_Extract()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Ace.stored.ace");
        using var stream = File.OpenRead(archivePath);
        using var reader = AceReader.Open(stream, new ReaderOptions());

        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }

        // Verify that files were extracted
        var extractedFiles = Directory.GetFiles(
            SCRATCH_FILES_PATH,
            "*.*",
            SearchOption.AllDirectories
        );
        Assert.True(extractedFiles.Length > 0, "Expected at least one file to be extracted");
    }

    [Fact]
    public void Ace_Factory_Detection()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Ace.stored.ace");
        using var stream = File.OpenRead(archivePath);

        var aceFactory = new AceFactory();
        Assert.True(aceFactory.IsArchive(stream));
    }

    [Fact]
    public void Ace_Reader_Properties()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Ace.stored.ace");
        using var stream = File.OpenRead(archivePath);
        using var reader = AceReader.Open(stream, new ReaderOptions());

        Assert.Equal(ArchiveType.Ace, reader.ArchiveType);
        Assert.NotNull(reader.Volume);

        if (reader.MoveToNextEntry())
        {
            Assert.Equal("test.txt", reader.Entry.Key);
            Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
            Assert.False(reader.Entry.IsEncrypted);
            Assert.False(reader.Entry.IsDirectory);
            Assert.False(reader.Entry.IsSplitAfter);
        }
    }
}
