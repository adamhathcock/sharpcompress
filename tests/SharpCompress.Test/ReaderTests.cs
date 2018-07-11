using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test
{
    public class ReaderTests : TestBase
    {
        protected void Read(string testArchive, CompressionType expectedCompression)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);

            using (var stream = new NonDisposingStream(new ForwardOnlyStream(File.OpenRead(testArchive)), true))
            using (var reader = ReaderFactory.Open(stream, new ReaderOptions { LeaveStreamOpen = true }))
            {
                UseReader(this, reader, expectedCompression);
                stream.ThrowOnDispose = false;
            }
        }

        public static void UseReader(TestBase test, IReader reader, CompressionType expectedCompression)
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(reader.Entry.CompressionType, expectedCompression);
                    reader.WriteEntryToDirectory(test.SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            test.VerifyFiles();
        }
    }
}
