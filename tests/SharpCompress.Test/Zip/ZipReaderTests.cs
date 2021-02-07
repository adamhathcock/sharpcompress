using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Zip
{
    public class ZipReaderTests : ReaderTests
    {
        public ZipReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public async ValueTask Issue_269_Double_Skip()
        {
            var path = Path.Combine(TEST_ARCHIVES_PATH, "PrePostHeaders.zip");
            await using (Stream stream = new ForwardOnlyStream(File.OpenRead(path)))
            await using (IReader reader = await ReaderFactory.OpenAsync(stream))
            {
                int count = 0;
                while (await reader.MoveToNextEntryAsync())
                {
                    count++;
                    if (!reader.Entry.IsDirectory)
                    {
                        if (count % 2 != 0)
                        {
                            await reader.WriteEntryToAsync(Stream.Null);
                        }
                    }
                }
            }
        }

        [Fact]
        public async ValueTask Zip_Zip64_Streamed_Read()
        {
            await ReadAsync("Zip.zip64.zip", CompressionType.Deflate);
        }

        [Fact]
        public async ValueTask Zip_ZipX_Streamed_Read()
        {
            await ReadAsync("Zip.zipx", CompressionType.LZMA);
        }

        [Fact]
        public async ValueTask Zip_BZip2_Streamed_Read()
        {
            await ReadAsync("Zip.bzip2.dd.zip", CompressionType.BZip2);
        }
        [Fact]
        public async ValueTask Zip_BZip2_Read()
        {
            await ReadAsync("Zip.bzip2.zip", CompressionType.BZip2);
        }
        [Fact]
        public async ValueTask Zip_Deflate_Streamed2_Read()
        {
            await ReadAsync("Zip.deflate.dd-.zip", CompressionType.Deflate);
        }
        [Fact]
        public async ValueTask Zip_Deflate_Streamed_Read()
        {
            await ReadAsync("Zip.deflate.dd.zip", CompressionType.Deflate);
        }
        [Fact]
        public async ValueTask Zip_Deflate_Streamed_Skip()
        {
            await using (Stream stream = new ForwardOnlyStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            await using (IReader reader = await ReaderFactory.OpenAsync(stream))
            {
                int x = 0;
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        x++;
                        if (x % 2 == 0)
                        {
                            await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                         new ExtractionOptions()
                                                         {
                                                             ExtractFullPath = true,
                                                             Overwrite = true
                                                         });
                        }
                    }
                }
            }
        }
        [Fact]
        public async ValueTask Zip_Deflate_Read()
        {
            await ReadAsync("Zip.deflate.zip", CompressionType.Deflate);
        }
        [Fact]
        public async ValueTask Zip_Deflate64_Read()
        {
            await ReadAsync("Zip.deflate64.zip", CompressionType.Deflate64);
        }

        [Fact]
        public async ValueTask Zip_LZMA_Streamed_Read()
        {
            await ReadAsync("Zip.lzma.dd.zip", CompressionType.LZMA);
        }
        [Fact]
        public async ValueTask Zip_LZMA_Read()
        {
            await ReadAsync("Zip.lzma.zip", CompressionType.LZMA);
        }
        [Fact]
        public async ValueTask Zip_PPMd_Streamed_Read()
        {
            await ReadAsync("Zip.ppmd.dd.zip", CompressionType.PPMd);
        }
        [Fact]
        public async ValueTask Zip_PPMd_Read()
        {
            await ReadAsync("Zip.ppmd.zip", CompressionType.PPMd);
        }

        [Fact]
        public async ValueTask Zip_None_Read()
        {
            await ReadAsync("Zip.none.zip", CompressionType.None);
        }

        [Fact]
        public async ValueTask Zip_Deflate_NoEmptyDirs_Read()
        {
            await ReadAsync("Zip.deflate.noEmptyDirs.zip", CompressionType.Deflate);
        }

        [Fact]
        public async ValueTask Zip_BZip2_PkwareEncryption_Read()
        {
            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.pkware.zip")))
            await using (var reader = ZipReader.Open(stream, new ReaderOptions()
            {
                Password = "test"
            }))
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.BZip2, reader.Entry.CompressionType);
                        await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public async ValueTask Zip_Reader_Disposal_Test()
        {
            await using (TestStream stream = new TestStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            {
                await using (var reader = await ReaderFactory.OpenAsync(stream))
                {
                    while (await reader.MoveToNextEntryAsync())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                         new ExtractionOptions()
                                                         {
                                                             ExtractFullPath = true,
                                                             Overwrite = true
                                                         });
                        }
                    }
                }
                Assert.True(stream.IsDisposed);
            }
        }

        [Fact]
        public async ValueTask Zip_Reader_Disposal_Test2()
        {
            using (TestStream stream = new TestStream(File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.dd.zip"))))
            {
                var reader = await ReaderFactory.OpenAsync(stream);
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                     new ExtractionOptions()
                                                     {
                                                         ExtractFullPath = true,
                                                         Overwrite = true
                                                     });
                    }
                }
                Assert.False(stream.IsDisposed);
            }
        }

        [Fact]
        public async ValueTask Zip_LZMA_WinzipAES_Read()
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                                            {
                                                using (
                                                    Stream stream =
                                                        File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH,
                                                            "Zip.lzma.WinzipAES.zip")))
                                                await using (var reader = ZipReader.Open(stream, new ReaderOptions()
                                                {
                                                    Password = "test"
                                                }))
                                                {
                                                    while (await reader.MoveToNextEntryAsync())
                                                    {
                                                        if (!reader.Entry.IsDirectory)
                                                        {
                                                            Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                                                            await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                                new ExtractionOptions()
                                                                {
                                                                    ExtractFullPath = true,
                                                                    Overwrite = true
                                                                });
                                                        }
                                                    }
                                                }
                                                VerifyFiles();
                                            });
        }

        [Fact]
        public async ValueTask Zip_Deflate_WinzipAES_Read()
        {
            await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip")))
            await using (var reader = ZipReader.Open(stream, new ReaderOptions()
            {
                Password = "test"
            }))
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Unknown, reader.Entry.CompressionType);
                        await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH,
                                                    new ExtractionOptions()
                                                    {
                                                        ExtractFullPath = true,
                                                        Overwrite = true
                                                    });
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public async Task TestSharpCompressWithEmptyStream()
        {
            var expected = new Tuple<string, byte[]>[]
            {
                new Tuple<string, byte[]>("foo.txt", new byte[0]),
                new Tuple<string, byte[]>("foo2.txt", new byte[10])
            };

            await using (var memory = new MemoryStream())
            {
                Stream stream = new TestStream(memory, read: true, write: true, seek: false);

                await using (IWriter zipWriter = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
                {
                    await zipWriter.WriteAsync(expected[0].Item1, new MemoryStream(expected[0].Item2));
                    await zipWriter.WriteAsync(expected[1].Item1, new MemoryStream(expected[1].Item2));
                }

                stream = new MemoryStream(memory.ToArray());
                await File.WriteAllBytesAsync(Path.Combine(SCRATCH_FILES_PATH, "foo.zip"), memory.ToArray());

                await using (IReader zipReader = ZipReader.Open(new NonDisposingStream(stream, true)))
                {
                    var i = 0;
                    while (await zipReader.MoveToNextEntryAsync())
                    {
                        await using (EntryStream entry = zipReader.OpenEntryStream())
                        {
                            MemoryStream tempStream = new MemoryStream();
                            const int bufSize = 0x1000;
                            byte[] buf = new byte[bufSize];
                            int bytesRead = 0;
                            while ((bytesRead = entry.Read(buf, 0, bufSize)) > 0)
                            {
                                tempStream.Write(buf, 0, bytesRead);
                            }

                            Assert.Equal(expected[i].Item1, zipReader.Entry.Key);
                            Assert.Equal(expected[i].Item2, tempStream.ToArray());
                        }
                        i++;
                    }
                }
            }
        }

        [Fact]
        public async ValueTask Zip_None_Issue86_Streamed_Read()
        {
            var keys = new string[] { "Empty1", "Empty2", "Dir1/", "Dir2/", "Fake1", "Fake2", "Internal.zip" };

            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.issue86.zip")))
            await using (var reader = ZipReader.Open(stream))
            {
                foreach (var key in keys)
                {
                    await reader.MoveToNextEntryAsync();

                    Assert.Equal(reader.Entry.Key, key);

                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.None, reader.Entry.CompressionType);
                    }
                }

                Assert.False(await reader.MoveToNextEntryAsync());
            }
        }

    }
}
