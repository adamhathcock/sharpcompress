using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arc;
using Xunit;

namespace SharpCompress.Test.Arc
{
    public class ArcReaderTests : ReaderTests
    {
        public ArcReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public void Arc_Uncompressed_Read() => Read("Arc.uncompressed.arc", CompressionType.None);

        [Fact]
        public void Arc_Squeezed_Read()
        {
            ProcessArchive("Arc.squeezed.arc");
        }

        [Fact]
        public void Arc_Crunched_Read()
        {
            ProcessArchive("Arc.crunched.arc");
        }

        private void ProcessArchive(string archiveName)
        {
            // Process a given archive by its name
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, archiveName)))
            using (IReader reader = ArcReader.Open(stream))
            {
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
            }

            VerifyFilesByExtension();
        }
    }
}
