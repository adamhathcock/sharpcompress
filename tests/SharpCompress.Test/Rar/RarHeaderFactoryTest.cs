using System.IO;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar
{
    /// <summary>
    /// Summary description for RarFactoryReaderTest
    /// </summary>
    public class RarHeaderFactoryTest : TestBase
    {
        private readonly RarHeaderFactory rarHeaderFactory;

        public RarHeaderFactoryTest()
        {
            rarHeaderFactory = new RarHeaderFactory(
                StreamingMode.Seekable,
                new ReaderOptions { LeaveStreamOpen = true });
        }

        [Fact]
        public void Rar_ReadHeaders_RecognizeEncryptedFlag()
        {
            ReadEncryptedFlag("Rar.encrypted_filesAndHeader.rar", true);
        }

        [Fact]
        public void Rar5_ReadHeaders_RecognizeEncryptedFlag()
        {
            ReadEncryptedFlag("Rar5.encrypted_filesAndHeader.rar", true);
        }

        [Fact]
        public void Rar_ReadHeaders_RecognizeNoEncryptedFlag()
        {
            ReadEncryptedFlag("Rar.rar", false);
        }

        [Fact]
        public void Rar5_ReadHeaders_RecognizeNoEncryptedFlag()
        {
            ReadEncryptedFlag("Rar5.rar", false);
        }

        private void ReadEncryptedFlag(string testArchive, bool isEncrypted)
        {
            using (var stream = new FileStream(Path.Combine(TEST_ARCHIVES_PATH, testArchive), FileMode.Open, FileAccess.Read))
            {
                foreach (var header in rarHeaderFactory.ReadHeaders(stream))
                {
                    if (header.HeaderType == HeaderType.Archive || header.HeaderType == HeaderType.Crypt)
                    {
                        Assert.Equal(isEncrypted, rarHeaderFactory.IsEncrypted);
                        break;
                    }
                }
            }
        }
    }
}
