using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Ace;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Ace
{
    public class AceReaderAsyncTests : ReaderTests
    {
        public AceReaderAsyncTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public async ValueTask Ace_Uncompressed_Read_Async() =>
            await ReadAsync("Ace.store.ace", CompressionType.None);

        [Fact]
        public async ValueTask Ace_Encrypted_Read_Async()
        {
            var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
                ReadAsync("Ace.encrypted.ace")
            );
        }

        [Theory]
        [InlineData("Ace.method1.ace", CompressionType.AceLZ77)]
        [InlineData("Ace.method1-solid.ace", CompressionType.AceLZ77)]
        [InlineData("Ace.method2.ace", CompressionType.AceLZ77)]
        [InlineData("Ace.method2-solid.ace", CompressionType.AceLZ77)]
        public async ValueTask Ace_Unsupported_ShouldThrow_Async(
            string fileName,
            CompressionType compressionType
        )
        {
            var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
                ReadAsync(fileName, compressionType)
            );
        }

        [Theory]
        [InlineData("Ace.store.largefile.ace", CompressionType.None)]
        public async ValueTask Ace_LargeFileTest_Read_Async(
            string fileName,
            CompressionType compressionType
        )
        {
            await ReadForBufferBoundaryCheckAsync(fileName, compressionType);
        }

        [Fact]
        public async ValueTask Ace_Multi_Reader_Async()
        {
            var exception = await Assert.ThrowsAsync<MultiVolumeExtractionException>(() =>
                DoMultiReaderAsync(new[] { "Ace.store.split.ace", "Ace.store.split.c01" })
            );
        }

        private async Task ReadAsync(string testArchive, CompressionType expectedCompression)
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            using Stream stream = File.OpenRead(testArchive);
            await using var reader = ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(stream),
                new ReaderOptions()
            );
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
            VerifyFiles();
        }

        private async Task ReadForBufferBoundaryCheckAsync(
            string testArchive,
            CompressionType expectedCompression
        )
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            using Stream stream = File.OpenRead(testArchive);
            await using var reader = ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(stream),
                new ReaderOptions() { LookForHeader = false }
            );
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
            VerifyFiles();
        }

        private async Task DoMultiReaderAsync(string[] archiveNames)
        {
            var testArchives = archiveNames
                .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                .ToList();
            var streams = testArchives.Select(File.OpenRead).ToList();
            try
            {
                await using var reader = ReaderFactory.OpenAsyncReader(
                    new AsyncOnlyStream(streams.First())
                );
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        await reader.WriteEntryToDirectoryAsync(
                            SCRATCH_FILES_PATH,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                        );
                    }
                }
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }
        }
    }
}
