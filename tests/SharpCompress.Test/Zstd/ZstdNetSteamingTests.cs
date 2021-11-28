using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZstdSharp.Unsafe;

namespace ZstdSharp.Test
{
    public enum DataFill
    {
        Random,
        Sequential
    }

    internal static class DataGenerator
    {
        private static readonly Random Random = new(1234);

        public const int LargeBufferSize = 1024 * 1024;
        public const int SmallBufferSize = 1024;

        public static MemoryStream GetSmallStream(DataFill dataFill) => GetStream(SmallBufferSize, dataFill);
        public static MemoryStream GetLargeStream(DataFill dataFill) => GetStream(LargeBufferSize, dataFill);
        public static MemoryStream GetStream(int length, DataFill dataFill) => new(GetBuffer(length, dataFill));

        public static byte[] GetSmallBuffer(DataFill dataFill) => GetBuffer(SmallBufferSize, dataFill);
        public static byte[] GetLargeBuffer(DataFill dataFill) => GetBuffer(LargeBufferSize, dataFill);

        public static byte[] GetBuffer(int length, DataFill dataFill)
        {
            var buffer = new byte[length];
            if (dataFill == DataFill.Random)
                Random.NextBytes(buffer);
            else
            {
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = (byte) (i % 256);
            }

            return buffer;
        }
    }

    public class ZstdNetSteamingTests
    {
        [Fact]
        public void StreamingCompressionZeroAndOneByte()
        {
            var data = new byte[] {0, 0, 0, 1, 2, 3, 4, 0, 0, 0};

            var tempStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(tempStream))
            {
                compressionStream.Write(data, 0, 0);
                compressionStream.Write(ReadOnlySpan<byte>.Empty);
                compressionStream.WriteAsync(data, 0, 0).GetAwaiter().GetResult();
                compressionStream.WriteAsync(ReadOnlyMemory<byte>.Empty).GetAwaiter().GetResult();

                compressionStream.Write(data, 3, 1);
                compressionStream.Write(new ReadOnlySpan<byte>(data, 4, 1));
                compressionStream.Flush();
                compressionStream.WriteAsync(data, 5, 1).GetAwaiter().GetResult();
                compressionStream.WriteAsync(new ReadOnlyMemory<byte>(data, 6, 1)).GetAwaiter().GetResult();
                compressionStream.FlushAsync().GetAwaiter().GetResult();
            }

            tempStream.Seek(0, SeekOrigin.Begin);

            var result = new byte[data.Length];
            using (var decompressionStream = new DecompressionStream(tempStream))
            {
                Assert.Equal(0, decompressionStream.Read(result, 0, 0));
                Assert.Equal(0, decompressionStream.Read(Span<byte>.Empty));
                Assert.Equal(0, decompressionStream.ReadAsync(result, 0, 0).GetAwaiter().GetResult());
                Assert.Equal(0, decompressionStream.ReadAsync(Memory<byte>.Empty).GetAwaiter().GetResult());

                Assert.Equal(1, decompressionStream.Read(result, 3, 1));
                Assert.Equal(1, decompressionStream.Read(new Span<byte>(result, 4, 1)));
                Assert.Equal(1, decompressionStream.ReadAsync(result, 5, 1).GetAwaiter().GetResult());
                Assert.Equal(1, decompressionStream.ReadAsync(new Memory<byte>(result, 6, 1)).GetAwaiter().GetResult());
            }

            Assert.True(data.SequenceEqual(result));
        }


        [Theory]
        [InlineData(new byte[0], 0, 0)]
        [InlineData(new byte[] {1, 2, 3}, 1, 2)]
        [InlineData(new byte[] {1, 2, 3}, 0, 2)]
        [InlineData(new byte[] {1, 2, 3}, 1, 1)]
        [InlineData(new byte[] {1, 2, 3}, 0, 3)]
        public void StreamingCompressionSimpleWrite(byte[] data, int offset, int count)
        {
            var tempStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(tempStream))
                compressionStream.Write(data, offset, count);

            tempStream.Seek(0, SeekOrigin.Begin);

            var resultStream = new MemoryStream();
            using (var decompressionStream = new DecompressionStream(tempStream))
                decompressionStream.CopyTo(resultStream);

            var dataToCompress = new byte[count];
            Array.Copy(data, offset, dataToCompress, 0, count);

            Assert.True(dataToCompress.SequenceEqual(resultStream.ToArray()));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(9)]
        [InlineData(10)]
        public void StreamingDecompressionSimpleRead(int readCount)
        {
            var data = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};

            var tempStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(tempStream))
                compressionStream.Write(data, 0, data.Length);

            tempStream.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[data.Length];
            using (var decompressionStream = new DecompressionStream(tempStream))
            {
                int bytesRead;
                int totalBytesRead = 0;
                while ((bytesRead = decompressionStream.Read(buffer, totalBytesRead,
                    Math.Min(readCount, buffer.Length - totalBytesRead))) > 0)
                {
                    Assert.True(bytesRead <= readCount);
                    totalBytesRead += bytesRead;
                }

                Assert.Equal(data.Length, totalBytesRead);
            }

            Assert.True(data.SequenceEqual(buffer));
        }

        [Fact]
        public void StreamingDecompressionTruncatedInput()
        {
            var dataStream = DataGenerator.GetLargeStream(DataFill.Sequential);

            var resultStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(resultStream))
                dataStream.CopyTo(compressionStream);

            // truncate resultStream
            var truncatedStream =
                new MemoryStream(resultStream.ToArray(), 0, Math.Min(32, (int) resultStream.Length / 3));

            var exception = Record.Exception(() =>
            {
                using var decompressionStream = new DecompressionStream(truncatedStream);
                decompressionStream.CopyTo(resultStream);
            });
            Assert.True(exception is EndOfStreamException);
        }

        [Fact]
        public void StreamingCompressionFlushDataFromInternalBuffers()
        {
            var testBuffer = new byte[1];

            var tempStream = new MemoryStream();
            using var compressionStream = new CompressionStream(tempStream);
            compressionStream.Write(testBuffer, 0, testBuffer.Length);
            compressionStream.Flush();

            Assert.True(tempStream.Length > 0);
            tempStream.Seek(0, SeekOrigin.Begin);

            //NOTE: without ZSTD_endStream call on compression
            var resultStream = new MemoryStream();
            using (var decompressionStream = new DecompressionStream(tempStream))
                decompressionStream.CopyTo(resultStream);

            Assert.True(testBuffer.SequenceEqual(resultStream.ToArray()));
        }

        [Fact]
        public void CompressionImprovesWithDictionary()
        {
            var dict = TrainDict();

            var dataStream = DataGenerator.GetSmallStream(DataFill.Sequential);

            var normalResultStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(normalResultStream))
                dataStream.CopyTo(compressionStream);

            dataStream.Seek(0, SeekOrigin.Begin);

            var dictResultStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(dictResultStream))
            {
                compressionStream.LoadDictionary(dict);
                dataStream.CopyTo(compressionStream);
            }

            Assert.True(normalResultStream.Length > dictResultStream.Length);

            dictResultStream.Seek(0, SeekOrigin.Begin);

            var resultStream = new MemoryStream();
            using (var decompressionStream = new DecompressionStream(dictResultStream))
            {
                decompressionStream.LoadDictionary(dict);
                decompressionStream.CopyTo(resultStream);
            }

            Assert.True(dataStream.ToArray().SequenceEqual(resultStream.ToArray()));
        }

        [Fact]
        public void CompressionShrinksData()
        {
            var dataStream = DataGenerator.GetLargeStream(DataFill.Sequential);

            var resultStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(resultStream))
                dataStream.CopyTo(compressionStream);

            Assert.True(dataStream.Length > resultStream.Length);
        }

        [Fact]
        public void RoundTrip_BatchToStreaming()
        {
            var data = DataGenerator.GetLargeBuffer(DataFill.Sequential);

            byte[] compressed;
            using (var compressor = new Compressor())
                compressed = compressor.Wrap(data).ToArray();

            var resultStream = new MemoryStream();
            using (var decompressionStream = new DecompressionStream(new MemoryStream(compressed)))
                decompressionStream.CopyTo(resultStream);

            Assert.True(data.SequenceEqual(resultStream.ToArray()));
        }

        [Fact]
        public void RoundTrip_StreamingToBatch()
        {
            var dataStream = DataGenerator.GetLargeStream(DataFill.Sequential);

            var tempStream = new MemoryStream();
            using (var compressionStream = new CompressionStream(tempStream))
                dataStream.CopyTo(compressionStream);

            var resultBuffer = new byte[dataStream.Length];
            using (var decompressor = new Decompressor())
                Assert.Equal(dataStream.Length, decompressor.Unwrap(tempStream.ToArray(), resultBuffer, 0));

            Assert.True(dataStream.ToArray().SequenceEqual(resultBuffer));
        }

        [Theory, CombinatorialData]
        public void RoundTrip_StreamingToStreaming(
            [CombinatorialValues(false, true)] bool useDict, [CombinatorialValues(false, true)] bool advanced,
            [CombinatorialValues(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize,
                DataGenerator.LargeBufferSize + 1)]
            int zstdBufferSize,
            [CombinatorialValues(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize,
                DataGenerator.LargeBufferSize + 1)]
            int copyBufferSize)
        {
            var dict = useDict ? TrainDict() : null;
            var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

            const int offset = 1;
            var buffer = new byte[copyBufferSize + offset + 1];

            var tempStream = new MemoryStream();
            using (var compressionStream =
                new CompressionStream(tempStream, Compressor.DefaultCompressionLevel, zstdBufferSize))
            {
                compressionStream.LoadDictionary(dict);
                if (advanced)
                {
                    compressionStream.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, 11);
                    compressionStream.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 1);
                }

                int bytesRead;
                while ((bytesRead = testStream.Read(buffer, offset, copyBufferSize)) > 0)
                    compressionStream.Write(buffer, offset, bytesRead);
            }

            tempStream.Seek(0, SeekOrigin.Begin);

            var resultStream = new MemoryStream();
            using (var decompressionStream = new DecompressionStream(tempStream, zstdBufferSize))
            {
                decompressionStream.LoadDictionary(dict);
                if (advanced)
                {
                    decompressionStream.SetParameter(ZSTD_dParameter.ZSTD_d_windowLogMax, 11);
                }

                int bytesRead;
                while ((bytesRead = decompressionStream.Read(buffer, offset, copyBufferSize)) > 0)
                    resultStream.Write(buffer, offset, bytesRead);
            }

            Assert.True(testStream.ToArray().SequenceEqual(resultStream.ToArray()));
        }

        [Theory, CombinatorialData]
        public async Task RoundTrip_StreamingToStreamingAsync(
            [CombinatorialValues(false, true)] bool useDict, [CombinatorialValues(false, true)] bool advanced,
            [CombinatorialValues(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize,
                DataGenerator.LargeBufferSize + 1)]
            int zstdBufferSize,
            [CombinatorialValues(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize,
                DataGenerator.LargeBufferSize + 1)]
            int copyBufferSize)
        {
            var dict = useDict ? TrainDict() : null;
            var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

            const int offset = 1;
            var buffer = new byte[copyBufferSize + offset + 1];

            var tempStream = new MemoryStream();
            await using (var compressionStream =
                new CompressionStream(tempStream, Compressor.DefaultCompressionLevel, zstdBufferSize))
            {
                compressionStream.LoadDictionary(dict);
                if (advanced)
                {
                    compressionStream.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, 11);
                    compressionStream.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 1);
                }

                int bytesRead;
                while ((bytesRead = await testStream.ReadAsync(buffer, offset, copyBufferSize)) > 0)
                    await compressionStream.WriteAsync(buffer, offset, bytesRead);
            }

            tempStream.Seek(0, SeekOrigin.Begin);

            var resultStream = new MemoryStream();
            await using (var decompressionStream = new DecompressionStream(tempStream, zstdBufferSize))
            {
                decompressionStream.LoadDictionary(dict);
                if (advanced)
                {
                    decompressionStream.SetParameter(ZSTD_dParameter.ZSTD_d_windowLogMax, 11);
                }

                int bytesRead;
                while ((bytesRead = await decompressionStream.ReadAsync(buffer, offset, copyBufferSize)) > 0)
                    await resultStream.WriteAsync(buffer, offset, bytesRead);
            }

            Assert.True(testStream.ToArray().SequenceEqual(resultStream.ToArray()));
        }

        [Theory(Skip = "stress"), CombinatorialData]
        public void RoundTrip_StreamingToStreaming_Stress([CombinatorialValues(true, false)] bool useDict,
            [CombinatorialValues(true, false)] bool async)
        {
            long i = 0;
            var dict = useDict ? TrainDict() : null;
            Enumerable.Range(0, 10000)
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount * 4)
                .ForAll(n =>
                {
                    var testStream = DataGenerator.GetSmallStream(DataFill.Sequential);
                    var cBuffer = new byte[1 + (int) (n % (testStream.Length * 11))];
                    var dBuffer = new byte[1 + (int) (n % (testStream.Length * 13))];

                    var tempStream = new MemoryStream();
                    using (var compressionStream = new CompressionStream(tempStream, Compressor.DefaultCompressionLevel,
                        1 + (int) (n % (testStream.Length * 17))))
                    {
                        compressionStream.LoadDictionary(dict);
                        int bytesRead;
                        int offset = n % cBuffer.Length;
                        while ((bytesRead = testStream.Read(cBuffer, offset, cBuffer.Length - offset)) > 0)
                        {
                            if (async)
                                compressionStream.WriteAsync(cBuffer, offset, bytesRead).GetAwaiter().GetResult();
                            else
                                compressionStream.Write(cBuffer, offset, bytesRead);
                            if (Interlocked.Increment(ref i) % 100 == 0)
                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                        }
                    }

                    tempStream.Seek(0, SeekOrigin.Begin);

                    var resultStream = new MemoryStream();
                    using (var decompressionStream =
                        new DecompressionStream(tempStream, 1 + (int) (n % (testStream.Length * 19))))
                    {
                        decompressionStream.LoadDictionary(dict);
                        int bytesRead;
                        int offset = n % dBuffer.Length;
                        while ((bytesRead = async
                            ? decompressionStream.ReadAsync(dBuffer, offset, dBuffer.Length - offset).GetAwaiter()
                                .GetResult()
                            : decompressionStream.Read(dBuffer, offset, dBuffer.Length - offset)) > 0)
                        {
                            resultStream.Write(dBuffer, offset, bytesRead);
                            if (Interlocked.Increment(ref i) % 100 == 0)
                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                        }
                    }

                    Assert.True(testStream.ToArray().SequenceEqual(resultStream.ToArray()));
                });
        }

        private static byte[] TrainDict()
        {
            var trainingData = new byte[100][];
            for (int i = 0; i < trainingData.Length; i++)
                trainingData[i] = DataGenerator.GetSmallBuffer(DataFill.Sequential);
            return DictBuilder.TrainFromBuffer(trainingData);
        }
    }
}
