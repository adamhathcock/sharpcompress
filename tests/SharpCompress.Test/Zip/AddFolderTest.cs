namespace SharpCompress.Test.Zip
{
  using System.IO;

  using SharpCompress.Archives;
  using SharpCompress.Archives.Zip;
  using SharpCompress.Common;
  using SharpCompress.Writers.Zip;

  using Xunit;

  public class AddFolderTest
  {
    [Fact]
    public void When_subject_should_expectation()
    {
      using (var zip = ZipArchive.Create())
      {
        zip.AddDirectory(Path.Combine("Hallo", "Welt"));
        zip.SaveTo(@"D:\Hallo.zip", new ZipWriterOptions(CompressionType.Deflate) { ArchiveComment = "HalloWelt" });
      }
    }
  }
}
