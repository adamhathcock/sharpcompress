using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class GZipBenchmarks
{
    private byte[] _sourceData = null!;
    private byte[] _compressedData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create 100KB of test data
        _sourceData = new byte[100 * 1024];
        new Random(42).NextBytes(_sourceData);

        // Pre-compress for decompression benchmark
        using var compressStream = new MemoryStream();
        using (var gzipStream = new GZipStream(compressStream, CompressionMode.Compress))
        {
            gzipStream.Write(_sourceData, 0, _sourceData.Length);
        }
        _compressedData = compressStream.ToArray();
    }

    [Benchmark(Description = "GZip: Compress 100KB")]
    public void GZipCompress()
    {
        using var outputStream = new MemoryStream();
        using var gzipStream = new GZipStream(outputStream, CompressionMode.Compress);
        gzipStream.Write(_sourceData, 0, _sourceData.Length);
    }

    [Benchmark(Description = "GZip: Compress 100KB (Async)")]
    public async Task GZipCompressAsync()
    {
        using var outputStream = new MemoryStream();
        using var gzipStream = new GZipStream(outputStream, CompressionMode.Compress);
        await gzipStream.WriteAsync(_sourceData, 0, _sourceData.Length).ConfigureAwait(false);
    }

    [Benchmark(Description = "GZip: Decompress 100KB")]
    public void GZipDecompress()
    {
        using var inputStream = new MemoryStream(_compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        gzipStream.CopyTo(Stream.Null);
    }

    [Benchmark(Description = "GZip: Decompress 100KB (Async)")]
    public async Task GZipDecompressAsync()
    {
        using var inputStream = new MemoryStream(_compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        await gzipStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
    }
}
