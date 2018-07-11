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

            Read(testArchive, expectedCompression, true);
            Read(testArchive, expectedCompression, false);
            VerifyFiles();
        }

        private void Read(string testArchive, CompressionType expectedCompression, bool leaveStreamOpen)
        {
            using (var file = File.OpenRead(testArchive))
            {
                using (var protectedStream = new NonDisposingStream(new ForwardOnlyStream(file), throwOnDispose: true))
                {
                    using (var testStream = new TestStream(protectedStream))
                    {
                        using (var reader = ReaderFactory.Open(testStream, new ReaderOptions { LeaveStreamOpen = leaveStreamOpen }))
                        {
                            UseReader(reader, expectedCompression);
                            protectedStream.ThrowOnDispose = false;
                            Assert.False(testStream.IsDisposed, "{nameof(testStream)} prematurely closed");
                        }

                        // Boolean XOR -- If the stream should be left open (true), then the stream should not be diposed (false)
                        // and if the stream should be closed (false), then the stream should be disposed (true)
                        var message = $"{nameof(leaveStreamOpen)} is set to '{leaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
                        Assert.True(leaveStreamOpen != testStream.IsDisposed, message);
                    }
                }
            }
        }

        public void UseReader(IReader reader, CompressionType expectedCompression)
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(reader.Entry.CompressionType, expectedCompression);
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
        }
    }
}
