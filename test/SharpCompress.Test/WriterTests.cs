using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Test
{
    public class WriterTests : TestBase
    {
        private ArchiveType type;

        protected WriterTests(ArchiveType type)
        {
            this.type = type;
        }

        protected void Write(CompressionType compressionType, string archive, string archiveToVerifyAgainst)
        {
            ResetScratch();
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            {
                using (var writer = WriterFactory.Open(new NonDisposingStream(stream), type, new WriterOptions(compressionType)
                                                               {
                                                                   LeaveOpenStream = true
                                                               }))
                {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                if (!stream.CanWrite)
                {
                    throw new InvalidOperationException();
                }
            }
            CompareArchivesByPath(Path.Combine(SCRATCH2_FILES_PATH, archive),
               Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst));

            using (Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            using (var reader = ReaderFactory.Open(new NonDisposingStream(stream)))
            {
                reader.WriteAllToDirectory(SCRATCH_FILES_PATH, new ExtractOptions()
                {
                    ExtractFullPath = true
                });
            }
            VerifyFiles();
        }
    }
}
