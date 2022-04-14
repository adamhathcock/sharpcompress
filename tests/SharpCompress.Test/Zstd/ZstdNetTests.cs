using System;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;
using ZstdSharp.Unsafe;

namespace ZstdSharp.Test
{
    public class ZstdNetTests
    {
        public enum CompressionLevel
        {
            Default = 0,
            Min,
            Max
        }

        [Theory]
        [InlineData(false, CompressionLevel.Min)]
        [InlineData(true, CompressionLevel.Min)]
        [InlineData(false, CompressionLevel.Default)]
        [InlineData(true, CompressionLevel.Default)]
        [InlineData(false, CompressionLevel.Max)]
        [InlineData(true, CompressionLevel.Max)]
        public void CompressAndDecompress_workCorrectly(bool useDictionary, CompressionLevel level)
        {
            var data = GenerateSample();

            var dict = useDictionary ? BuildDictionary() : null;
            var compressionLevel = level switch
            {
                CompressionLevel.Min => Compressor.MinCompressionLevel,
                CompressionLevel.Max => Compressor.MaxCompressionLevel,
                _ => Compressor.DefaultCompressionLevel
            };

            Assert.True(CompressAndDecompress(data, dict, compressionLevel).SequenceEqual(data));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_worksCorrectly_advanced(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            Span<byte> compressed1, compressed2;

            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressor.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 0);
                compressed1 = compressor.Wrap(data);
            }

            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressor.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 1);
                compressed2 = compressor.Wrap(data);
            }

            Assert.Equal(compressed1.Length + 4, compressed2.Length);

            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                Assert.True(decompressor.Unwrap(compressed1).SequenceEqual(data));
                Assert.True(decompressor.Unwrap(compressed2).SequenceEqual(data));
            }
        }

        [Fact]
        public void DecompressWithDictionary_worksCorrectly_onDataCompressedWithoutIt()
        {
            var data = GenerateSample();
            Span<byte> compressed;
            using (var compressor = new Compressor())
                compressed = compressor.Wrap(data);

            var dict = BuildDictionary();

            Span<byte> decompressed;
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                decompressed = decompressor.Unwrap(compressed);
            }

            Assert.True(decompressed.SequenceEqual(data));
        }

        [Fact]
        public void DecompressWithoutDictionary_throwsZstdException_onDataCompressedWithIt()
        {
            var data = GenerateSample();
            var dict = BuildDictionary();

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            using (var decompressor = new Decompressor())
            {
                Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed));
            }
        }

        [Fact]
        public void DecompressWithAnotherDictionary_throwsZstdException()
        {
            var data = GenerateSample();
            var oldDict = BuildDictionary();

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(oldDict);
                compressed = compressor.Wrap(data).ToArray();
            }

            var newDict = Encoding.ASCII.GetBytes("zstd supports raw-content dictionaries");

            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(newDict);
                Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed));
            }
        }

        [Fact]
        public void Compress_reducesDataSize()
        {
            var data = GenerateSample();

            Span<byte> compressed;
            using (var compressor = new Compressor())
                compressed = compressor.Wrap(data);

            Assert.True(data.Length > compressed.Length);
        }

        [Fact]
        public void Compress_worksBetter_withDictionary()
        {
            var data = GenerateSample();

            Span<byte> compressedWithoutDict, compressedWithDict;
            using (var compressor = new Compressor())
                compressedWithoutDict = compressor.Wrap(data);

            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(BuildDictionary());
                compressedWithDict = compressor.Wrap(data);
            }

            Assert.True(compressedWithoutDict.Length > compressedWithDict.Length);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_throwsZstdException_onInvalidData(bool useDictionary)
        {
            var data = GenerateSample(); // This isn't data in compressed format
            var dict = useDictionary ? BuildDictionary() : null;

            using var decompressor = new Decompressor();
            decompressor.LoadDictionary(dict);
            Assert.Throws<ZstdException>(() => decompressor.Unwrap(data));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_throwsZstdException_onMalformedDecompressedSize(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            var frameHeader = compressed[4]; // Ensure that we malform decompressed size in the right place
            if (useDictionary)
            {
                Assert.Equal(0x63, frameHeader);
                compressed[9]--;
            }
            else
            {
                Assert.Equal(0x60, frameHeader);
                compressed[5]--;
            }

            // Thus, ZSTD_getDecompressedSize will return size that is one byte lesser than actual
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_throwsArgumentOutOfRangeException_onTooBigData(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                var ex = Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed, 20));
                Assert.Equal(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, ex.Code);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_tryUnwrap_onTooBigData(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                var dest = new byte[20];
                Assert.False(decompressor.TryUnwrap(compressed, dest, out _));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compress_canRead_fromArraySegment(bool useDictionary)
        {
            var data = GenerateSample();
            var segment = new ArraySegment<byte>(data, 2, data.Length - 5);
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(segment).ToArray();
            }

            byte[] decompressed;
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                decompressed = decompressor.Unwrap(compressed).ToArray();
            }

            Assert.True(segment.SequenceEqual(decompressed));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_spans(bool useDictionary)
        {
            var buffer = GenerateSample();

            var data = new ReadOnlySpan<byte>(buffer, 1, buffer.Length - 1);
            var dict = useDictionary ? BuildDictionary() : null;

            Span<byte> compressed = stackalloc byte[Compressor.GetCompressBound(data.Length)];
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                var size = compressor.Wrap(data, compressed);
                compressed = compressed.Slice(0, size);
            }

            Span<byte> decompressed = stackalloc byte[data.Length + 1];
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                var size = decompressor.Unwrap(compressed, decompressed);
                Assert.Equal(data.Length, size);
                decompressed = decompressed.Slice(0, size);
            }

            Assert.True(data.ToArray().SequenceEqual(decompressed.ToArray()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_canRead_fromArraySegment(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            compressed = new byte[] { 1, 2 }.Concat(compressed).Concat(new byte[] { 4, 5, 6 }).ToArray();
            var segment = new ArraySegment<byte>(compressed, 2, compressed.Length - 5);

            byte[] decompressed;
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                decompressed = decompressor.Unwrap(segment).ToArray();
            }

            Assert.True(data.SequenceEqual(decompressed));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compress_canWrite_toGivenBuffer(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;
            var compressed = new byte[1000];
            const int offset = 54;

            int compressedSize;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressedSize = compressor.Wrap(data, compressed, offset);
            }

            byte[] decompressed;
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                decompressed = decompressor.Unwrap(compressed.Skip(offset).Take(compressedSize).ToArray()).ToArray();
            }

            Assert.True(data.SequenceEqual(decompressed));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_canWrite_toGivenBuffer(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            var decompressed = new byte[1000];
            const int offset = 54;

            int decompressedSize;
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                decompressedSize = decompressor.Unwrap(compressed, decompressed, offset);
            }

            Assert.True(data.SequenceEqual(decompressed.Skip(offset).Take(decompressedSize)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compress_throwsDstSizeTooSmall_whenDestinationBufferIsTooSmall(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;
            var compressed = new byte[20];
            const int offset = 4;

            using var compressor = new Compressor();
            compressor.LoadDictionary(dict);
            var ex = Assert.Throws<ZstdException>(() => compressor.Wrap(data, compressed, offset));
            Assert.Equal(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, ex.Code);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compress_tryWrap_whenDestinationBufferIsTooSmall(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;
            var compressed = new byte[20];
            const int offset = 4;

            using var compressor = new Compressor();
            compressor.LoadDictionary(dict);
            Assert.False(compressor.TryWrap(data, compressed, offset, out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Decompress_throwsDstSizeTooSmall_whenDestinationBufferIsTooSmall(bool useDictionary)
        {
            var data = GenerateSample();
            var dict = useDictionary ? BuildDictionary() : null;

            byte[] compressed;
            using (var compressor = new Compressor())
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data).ToArray();
            }

            var decompressed = new byte[20];
            const int offset = 4;

            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                var ex = Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed, decompressed, offset));
                Assert.Equal(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, ex.Code);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_onEmptyBuffer(bool useDictionary)
        {
            var data = new byte[0];
            var dict = useDictionary ? BuildDictionary() : null;

            Assert.True(CompressAndDecompress(data, dict).SequenceEqual(data));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_onOneByteBuffer(bool useDictionary)
        {
            var data = new byte[] { 42 };
            var dict = useDictionary ? BuildDictionary() : null;

            Assert.True(CompressAndDecompress(data, dict).SequenceEqual(data));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_onArraysOfDifferentSizes(bool useDictionary)
        {
            var dict = useDictionary ? BuildDictionary() : null;
            using var compressor = new Compressor();
            using var decompressor = new Decompressor();
            compressor.LoadDictionary(dict);
            decompressor.LoadDictionary(dict);
            for (var i = 2; i < 100000; i += 3000)
            {
                var data = GenerateBuffer(i);

                var decompressed = decompressor.Unwrap(compressor.Wrap(data)).ToArray();

                Assert.True(data.SequenceEqual(decompressed));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_ifDifferentInstancesRunInDifferentThreads(bool useDictionary)
        {
            var dict = useDictionary ? BuildDictionary() : null;
            Enumerable.Range(0, 100)
                .AsParallel().WithDegreeOfParallelism(50)
                .ForAll(_ =>
                {
                    using var compressor = new Compressor();
                    using var decompressor = new Decompressor();
                    compressor.LoadDictionary(dict);
                    decompressor.LoadDictionary(dict);

                    for (var i = 2; i < 100000; i += 30000)
                    {
                        var data = GenerateBuffer(i);

                        var decompressed = decompressor.Unwrap(compressor.Wrap(data));

                        Assert.True(decompressed.SequenceEqual(data));
                    }
                });
        }

        [Theory(Skip = "Explicit/stress")]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_stress(bool useDictionary)
        {
            long i = 0L;
            var data = GenerateBuffer(65536);
            var dict = useDictionary ? BuildDictionary() : null;
            Enumerable.Range(0, 10000)
                .AsParallel().WithDegreeOfParallelism(100)
                .ForAll(_ =>
                {
                    using var compressor = new Compressor();
                    using var decompressor = new Decompressor();
                    compressor.LoadDictionary(dict);
                    decompressor.LoadDictionary(dict);
                    var decompressed = decompressor.Unwrap(compressor.Wrap(data));
                    if (Interlocked.Increment(ref i) % 100 == 0)
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    Assert.True(decompressed.SequenceEqual(data));
                });
        }

        [Theory(Skip = "Explicit/memory consuming")]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_workCorrectly_2GB(bool useDictionary)
        {
            var data = new byte[MaxByteArrayLength];
            new Span<byte>(data, 100, 10000000).Fill(0xff);

            var dict = useDictionary ? BuildDictionary() : null;

            Assert.True(CompressAndDecompress(data, dict).SequenceEqual(data));

            int size;
            //NOTE: Calc max reliable compression data size
            for (size = MaxByteArrayLength; Compressor.GetCompressBoundLong((ulong)size) > MaxByteArrayLength; size--)
            {
            }

            data = new byte[size];
            new Random(1337).NextBytes(data); //NOTE: Uncompressible data

            Assert.True(CompressAndDecompress(data, dict).SequenceEqual(data));
        }

        [Theory(Skip = "Explicit/memory consuming")]
        [InlineData(false)]
        [InlineData(true)]
        public void CompressAndDecompress_throwsDstSizeTooSmall_Over2GB(bool useDictionary)
        {
            var data = new byte[MaxByteArrayLength];
            new Random(1337).NextBytes(data); //NOTE: Uncompressible data

            var dict = useDictionary ? BuildDictionary() : null;

            using var compressor = new Compressor();
            compressor.LoadDictionary(dict);
            var ex = Assert.Throws<ZstdException>(() => compressor.Wrap(data));
            Assert.Equal(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, ex.Code);
        }

        [Fact(Skip = "Explicit/stress")]
        public void TrainDictionaryParallel()
        {
            var buffer = Enumerable.Range(0, 100000).Select(i => unchecked((byte)(i * i))).ToArray();
            var samples = Enumerable.Range(0, 100)
                .Select(i => buffer.Skip(i).Take(200 - i).ToArray())
                .ToArray();

            var dict = DictBuilder.TrainFromBuffer(samples);
            Assert.True(dict.Length > 0);
            Assert.True(dict.Length <= DictBuilder.DefaultDictCapacity);

            Enumerable.Range(0, 100000)
                .AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount * 4)
                .ForAll(_ => Assert.True(dict.SequenceEqual(DictBuilder.TrainFromBuffer(samples))));
        }

        private static byte[] BuildDictionary()
            => DictBuilder.TrainFromBuffer(Enumerable.Range(0, 8).Select(_ => GenerateSample()).ToArray(), 1024);

        private static byte[] GenerateSample()
        {
            var random = new Random(1234);
            return Enumerable.Range(0, 10)
                .SelectMany(_ =>
                    Encoding.ASCII.GetBytes(
                        $"['a': 'constant_field', 'b': '{random.Next()}', 'c': {random.Next()}, 'd': '{(random.Next(1) == 1 ? "almost" : "sometimes")} constant field']"))
                .ToArray();
        }

        private static byte[] GenerateBuffer(int size)
        {
            return Enumerable.Range(0, size)
                .Select(i => unchecked((byte) i))
                .ToArray();
        }

        private static Span<byte> CompressAndDecompress(byte[] data, byte[] dict,
            int compressionLevel = Compressor.DefaultCompressionLevel)
        {
            Span<byte> compressed;
            using (var compressor = new Compressor(compressionLevel))
            {
                compressor.LoadDictionary(dict);
                compressed = compressor.Wrap(data);
            }

            Span<byte> decompressed;
            using (var decompressor = new Decompressor())
            {
                decompressor.LoadDictionary(dict);
                decompressed = decompressor.Unwrap(compressed);
            }

            return decompressed;
        }

        private const int MaxByteArrayLength = 0x7FFFFFC7;
    }
}
