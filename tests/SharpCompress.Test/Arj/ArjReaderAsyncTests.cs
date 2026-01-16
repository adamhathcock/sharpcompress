using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arj;
using SharpCompress.Test.Mocks;
using Xunit;
using Xunit.Sdk;

namespace SharpCompress.Test.Arj
{
    public class ArjReaderAsyncTests : ReaderTests
    {
        public ArjReaderAsyncTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
            UseCaseInsensitiveToVerify = true;
        }

        [Fact]
        public async ValueTask Arj_Uncompressed_Read_Async() =>
            await ReadAsync("Arj.store.arj", CompressionType.None);

        [Fact]
        public async ValueTask Arj_Method1_Read_Async() => await ReadAsync("Arj.method1.arj");

        [Fact]
        public async ValueTask Arj_Method2_Read_Async() => await ReadAsync("Arj.method2.arj");

        [Fact]
        public async ValueTask Arj_Method3_Read_Async() => await ReadAsync("Arj.method3.arj");

        [Fact]
        public async ValueTask Arj_Method4_Read_Async() => await ReadAsync("Arj.method4.arj");

        [Fact]
        public async ValueTask Arj_Encrypted_Read_Async()
        {
            var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
                ReadAsync("Arj.encrypted.arj")
            );
        }

        [Fact]
        public async ValueTask Arj_Multi_Reader_Async()
        {
            var exception = await Assert.ThrowsAsync<MultiVolumeExtractionException>(() =>
                DoMultiReaderAsync(
                    [
                        "Arj.store.split.arj",
                        "Arj.store.split.a01",
                        "Arj.store.split.a02",
                        "Arj.store.split.a03",
                        "Arj.store.split.a04",
                        "Arj.store.split.a05",
                    ],
                    streams => ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(streams.First()))
                )
            );
        }

        [Theory]
        [InlineData("Arj.method1.largefile.arj", CompressionType.ArjLZ77)]
        [InlineData("Arj.method2.largefile.arj", CompressionType.ArjLZ77)]
        [InlineData("Arj.method3.largefile.arj", CompressionType.ArjLZ77)]
        public async ValueTask Arj_LargeFile_ShouldThrow_Async(
            string fileName,
            CompressionType compressionType
        )
        {
            var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
                ReadForBufferBoundaryCheckAsync(fileName, compressionType)
            );
        }

        [Theory]
        [InlineData("Arj.store.largefile.arj", CompressionType.None)]
        [InlineData("Arj.method4.largefile.arj", CompressionType.ArjLZ77)]
        public async ValueTask Arj_LargeFileTest_Read_Async(
            string fileName,
            CompressionType compressionType
        )
        {
            await ReadForBufferBoundaryCheckAsync(fileName, compressionType);
        }

        private async Task ReadAsync(
            string testArchive,
            CompressionType? expectedCompression = null
        )
        {
            testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
            using Stream stream = File.OpenRead(testArchive);
            await using var reader = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(stream),
                new ReaderOptions()
            );
            while (await reader.MoveToNextEntryAsync())
            {
                if (!reader.Entry.IsDirectory)
                {
                    if (expectedCompression.HasValue)
                    {
                        Assert.Equal(expectedCompression.Value, reader.Entry.CompressionType);
                    }
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
            await using var reader = await ReaderFactory.OpenAsyncReader(
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

        private async Task DoMultiReaderAsync(
            string[] archiveNames,
            Func<IEnumerable<Stream>, ValueTask<IAsyncReader>> openReader
        )
        {
            var testArchives = archiveNames
                .Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                .ToList();
            var streams = testArchives.Select(File.OpenRead).ToList();
            try
            {
                await using var reader = await openReader(streams);
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
