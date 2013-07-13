using System;
using System.IO;
using System.Net.Security;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Test.Rar.Unit
{
    /// <summary>
    /// Summary description for RarFactoryReaderTest
    /// </summary>
    [TestClass]
    public class RarHeaderFactoryTest : TestBase
    {
        private RarHeaderFactory rarHeaderFactory;

        [TestInitialize]
        public void Initialize()
        {
            ResetScratch();
            rarHeaderFactory = new RarHeaderFactory(StreamingMode.Seekable, Options.KeepStreamsOpen);
        }

        
        [TestMethod]
        public void ReadHeaders_RecognizeEncryptedFlag()
        {

            ReadEncryptedFlag("Rar.Encrypted.rar", true);
            
            

        }

        private void ReadEncryptedFlag(string testArchive, bool isEncrypted)
        {
            using (var stream = GetReaderStream(testArchive))
                foreach (var header in rarHeaderFactory.ReadHeaders(stream))
                {
                    if (header.HeaderType == HeaderType.ArchiveHeader)
                    {
                        Assert.AreEqual(isEncrypted, rarHeaderFactory.IsEncrypted);
                        break;
                    }
                }
        }

        [TestMethod]
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
