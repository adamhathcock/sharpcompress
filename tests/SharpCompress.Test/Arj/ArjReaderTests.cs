using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arj;
using Xunit;

namespace SharpCompress.Test.Arj
{
    public class ArjReaderTests : ReaderTests
    {
        public ArjReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public void Arj_Uncompressed_Read() => Read("Arj.store.arj", CompressionType.None);

        [Fact]
        public void Arj_Method4_Read() => Read("Arj.method4.arj");

        [Fact]
        public void Arj_Multi_Reader()
        {
            var exception = Assert.Throws<MultiVolumeExtractionException>(() =>
                DoArj_Multi_Reader(
                    [
                        "Arj.store.split.arj",
                        "Arj.store.split.a01",
                        "Arj.store.split.a02",
                        "Arj.store.split.a03",
                        "Arj.store.split.a04",
                        "Arj.store.split.a05",
                    ]
                )
            );
        }

        private void DoArj_Multi_Reader(string[] archives)
        {
            using (
                var reader = ArjReader.Open(
                    archives
                        .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                        .Select(p => File.OpenRead(p))
                )
            )
            {
                while (reader.MoveToNextEntry())
                {
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
            VerifyFiles();
        }
    }
}
