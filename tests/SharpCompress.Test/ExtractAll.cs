using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

public class ExtractAllTests : TestBase
{
    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar.rar")]
    [InlineData("Rar.solid.rar")]
    [InlineData("7Zip.solid.7z")]
    [InlineData("7Zip.nonsolid.7z")]
    [InlineData("7Zip.LZMA.7z")]
    public async Task ExtractAllEntriesAsync(string archivePath)
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, archivePath);
        var options = new ExtractionOptions() { ExtractFullPath = true, Overwrite = true };

        using var archive = ArchiveFactory.Open(testArchive);
        await archive.WriteToDirectoryAsync(SCRATCH_FILES_PATH, options);
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
        archive.WriteToDirectory(SCRATCH_FILES_PATH, options);
    }
}
