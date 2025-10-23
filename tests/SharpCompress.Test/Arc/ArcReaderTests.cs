using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arc;
using Xunit;

namespace SharpCompress.Test.Arc
{
    public class ArcReaderTests
        : ReaderTests { /*
        public ArcReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public void Arc_Uncompressed_Read() => Read("Arc.uncompressed.arc", CompressionType.None);

        [Fact]
        public async Task Arc_Squeezed_Read()
        {
            await ProcessArchive("Arc.squeezed.arc");
        }

        [Fact]
        public async Task Arc_Crunched_Read()
        {
            await ProcessArchive("Arc.crunched.arc");
        }

        private async Task ProcessArchive(string archiveName)
        {
            // Process a given archive by its name
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archiveName)))
            using (IReader reader = ArcReader.Open(stream))
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        await reader.WriteAllToDirectoryAsync(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                }
            }

            VerifyFilesByExtension();
        }*/
    }
}
