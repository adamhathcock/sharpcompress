using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipArchiveTests : ArchiveTests
{
  public GZipArchiveTests() => UseExtensionInsteadOfNameToVerify = true;

  [Fact]
  public void GZip_Archive_Generic()
  {
    using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
    using (var archive = ArchiveFactory.Open(stream))
    {
      var entry = archive.Entries.First();
      entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

      var size = entry.Size;
      var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
      var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

      Assert.Equal(size, scratch.Length);
      Assert.Equal(size, test.Length);
    }
    CompareArchivesByPath(
      Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
      Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")
    );
  }

  [Fact]
  public void GZip_Archive()
  {
    using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
    using (var archive = GZipArchive.Open(stream))
    {
      var entry = archive.Entries.First();
      entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

      var size = entry.Size;
      var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
      var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

      Assert.Equal(size, scratch.Length);
      Assert.Equal(size, test.Length);
    }
    CompareArchivesByPath(
      Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
      Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")
    );
  }

  [Fact]
  public void GZip_Archive_NoAdd()
  {
    var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
    using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
    using var archive = GZipArchive.Open(stream);
    Assert.Throws<InvalidFormatException>(() => archive.AddEntry("jpg\\test.jpg", jpg));
    archive.SaveTo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"));
  }

  [Fact]
  public void GZip_Archive_Multiple_Reads()
  {
    var inputStream = new MemoryStream();
    using (var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
    {
      fileStream.CopyTo(inputStream);
      inputStream.Position = 0;
    }
    using var archive = GZipArchive.Open(inputStream);
    var archiveEntry = archive.Entries.First();

    MemoryStream tarStream;
    using (var entryStream = archiveEntry.OpenEntryStream())
    {
      tarStream = new MemoryStream();
      entryStream.CopyTo(tarStream);
    }
    var size = tarStream.Length;
    using (var entryStream = archiveEntry.OpenEntryStream())
    {
      tarStream = new MemoryStream();
      entryStream.CopyTo(tarStream);
    }
    Assert.Equal(size, tarStream.Length);
    using (var entryStream = archiveEntry.OpenEntryStream())
    {
      var result = TarArchive.IsTarFile(entryStream);
      Assert.True(result);
    }
    Assert.Equal(size, tarStream.Length);
    using (var entryStream = archiveEntry.OpenEntryStream())
    {
      tarStream = new MemoryStream();
      entryStream.CopyTo(tarStream);
    }
    Assert.Equal(size, tarStream.Length);
  }

  [Fact]
  public void TestGzCrcWithMostSignificantBitNotNegative()
  {
    using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
    using var archive = GZipArchive.Open(stream);
    //process all entries in solid archive until the one we want to test
    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
    {
      Assert.InRange(entry.Crc, 0L, 0xFFFFFFFFL);
    }
  }

  [Fact]
  public void TestGzArchiveTypeGzip()
  {
    using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
    using var archive = GZipArchive.Open(stream);
    Assert.Equal(ArchiveType.GZip, archive.Type);
  }
}
