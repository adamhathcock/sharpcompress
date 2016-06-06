using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Rar
{
    /// <summary>
    /// Summary description for RarFactoryReaderTest
    /// </summary>
    public class RarHeaderFactoryTest : TestBase
    {
        private RarHeaderFactory rarHeaderFactory;
        
        public RarHeaderFactoryTest()
        {
            ResetScratch();
            rarHeaderFactory = new RarHeaderFactory(StreamingMode.Seekable, Options.KeepStreamsOpen);
        }


        [Fact]
        public void ReadHeaders_RecognizeEncryptedFlag()
        {

            ReadEncryptedFlag("Rar.Encrypted_filesAndHeader.rar", true);



        }

        private void ReadEncryptedFlag(string testArchive, bool isEncrypted)
        {
            using (var stream = GetReaderStream(testArchive))
                foreach (var header in rarHeaderFactory.ReadHeaders(stream))
                {
                    if (header.HeaderType == HeaderType.ArchiveHeader)
                    {
                        Assert.Equal(isEncrypted, rarHeaderFactory.IsEncrypted);
                        break;
                    }
                }
        }

        [Fact]
        public void ReadHeaders_RecognizeNoEncryptedFlag()
        {
            ReadEncryptedFlag("Rar.rar", false);
        }

        private FileStream GetReaderStream(string testArchive)
        {
            return new FileStream(Path.Combine(TEST_ARCHIVES_PATH, testArchive),
                                  FileMode.Open);
        }
    }
}
