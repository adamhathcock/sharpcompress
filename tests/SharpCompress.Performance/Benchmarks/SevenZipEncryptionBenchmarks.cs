using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Writers.SevenZip;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class SevenZipEncryptionBenchmarks
{
    private byte[] inputBytes = null!;
    private string[] names = null!;
    private SevenZipWriterOptions options = null!;

    [Params(CompressionType.LZMA, CompressionType.LZMA2)]
    public CompressionType Codec { get; set; }

    [Params("None", "Data", "DataAndHeader")]
    public string Encryption { get; set; } = null!;

    [Params(false, true)]
    public bool ManySmallFiles { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        inputBytes = new byte[ManySmallFiles ? 128 : 262144];
        new Random(1476).NextBytes(inputBytes);
        names = new string[ManySmallFiles ? 100 : 1];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = $"file{i}";
        }
        options = new SevenZipWriterOptions(Codec)
        {
            Password = Encryption == "None" ? null : "profile",
            EncryptHeader = Encryption == "DataAndHeader",
            LeaveStreamOpen = true,
            LzmaProperties = new LzmaEncoderProperties(true, 4096),
        };
    }

    [Benchmark]
    public long Write()
    {
        // Include archive construction/finalization, but avoid output-growth noise.
        using var output = new MemoryStream(1048576);
        using (var writer = new SevenZipWriter(output, options))
        {
            foreach (var name in names)
            {
                using var input = new MemoryStream(inputBytes, writable: false);
                writer.Write(name, input, null);
            }
        }
        return output.Length;
    }

    [Benchmark]
    public async Task<long> WriteAsync()
    {
        using var output = new MemoryStream(1048576);
        await using (var writer = new SevenZipWriter(output, options))
        {
            foreach (var name in names)
            {
                using var input = new MemoryStream(inputBytes, writable: false);
                await writer
                    .WriteAsync(name, input, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        return output.Length;
    }
}
