using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

public class ExtractAll : TestBase
{
    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar.rar")]
    [InlineData("Rar.solid.rar")]
    [InlineData("7Zip.solid.7z")]
    [InlineData("7Zip.nonsolid.7z")]
    [InlineData("7Zip.LZMA.7z")]
    public async Task ExtractAllEntries(string archivePath)
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, archivePath);
        var options = new ExtractionOptions() { ExtractFullPath = true, Overwrite = true };

        using var archive = ArchiveFactory.Open(testArchive);

        if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH, options);
                }
            }
        }
        else
        {
            archive.ExtractToDirectory(SCRATCH_FILES_PATH, options);
        }
    }

    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar.rar")]
    [InlineData("Rar.solid.rar")]
    [InlineData("7Zip.solid.7z")]
    [InlineData("7Zip.nonsolid.7z")]
    [InlineData("7Zip.LZMA.7z")]
    public void ExtractAllEntriesSync(string archivePath)
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, archivePath);
        var options = new ExtractionOptions() { ExtractFullPath = true, Overwrite = true };

        using var archive = ArchiveFactory.Open(testArchive);

        if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, options);
                }
            }
        }
        else
        {
            archive.ExtractToDirectory(SCRATCH_FILES_PATH, options);
        }
    }
}
