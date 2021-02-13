using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test
{
    public abstract class ReaderTests : TestBase
    {
        protected async ValueTask ReadAsync(string testArchive, CompressionType expectedCompression, ReaderOptions options = null)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);

            options ??= new ReaderOptions();

            options.LeaveStreamOpen = true;
            await ReadImplAsync(testArchive, expectedCompression, options);

            options.LeaveStreamOpen = false;
            await ReadImplAsync(testArchive, expectedCompression, options);
            VerifyFiles();
        }

        private async ValueTask ReadImplAsync(string testArchive, CompressionType expectedCompression, ReaderOptions options)
        {
            await using (var file = File.OpenRead(testArchive))
            {
                await using (var protectedStream = new NonDisposingStream(new ForwardOnlyStream(file), throwOnDispose: true))
                {
                    await using (var testStream = new TestStream(protectedStream))
                    {
                        await using (var reader = await ReaderFactory.OpenAsync(testStream, options))
                        {
                            await ReadAsync(reader, expectedCompression);
                            protectedStream.ThrowOnDispose = false;
                            Assert.False(testStream.IsDisposed, "{nameof(testStream)} prematurely closed");
                        }

                        // Boolean XOR -- If the stream should be left open (true), then the stream should not be diposed (false)
                        // and if the stream should be closed (false), then the stream should be disposed (true)
                        var message = $"{nameof(options.LeaveStreamOpen)} is set to '{options.LeaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
                        Assert.True(options.LeaveStreamOpen != testStream.IsDisposed, message);
                    }
                }
            }
        }

        public async ValueTask ReadAsync(IReader reader, CompressionType expectedCompression)
        {
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
        }
    }
}
