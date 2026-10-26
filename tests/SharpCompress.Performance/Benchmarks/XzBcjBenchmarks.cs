using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.Xz.Filters;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class XzBcjBenchmarks
{
    private byte[] inputBytes = null!;
    private byte[] buffer = null!;

    [Params("X86", "ARM", "ARMThumb", "PowerPC", "SPARC", "IA64", "Xz")]
    public string Layer { get; set; } = null!;

    [Params(1, 16, 10240, 1048576)]
    public int ReadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        buffer = new byte[ReadSize + 5];
        if (Layer != "Xz")
        {
            // Already BCJ-encoded instructions; isolate filtering from LZMA2 and checks.
            var pattern = Convert.FromHexString(
                Layer switch
                {
                    "X86" => "9090e80000010090e900000200909090",
                    "ARM" => "010000eb",
                    "ARMThumb" => "00f000f8",
                    "PowerPC" => "48000001",
                    "SPARC" => "40000000",
                    "IA64" => "5648b22039e844808c9e2cfc290e2076",
                    _ => throw new InvalidOperationException("Unknown BCJ architecture"),
                }
            );
            inputBytes = new byte[1024 * 1024];
            for (var offset = 0; offset < inputBytes.Length; offset += pattern.Length)
            {
                Buffer.BlockCopy(pattern, 0, inputBytes, offset, pattern.Length);
            }
        }
        else
        {
            var samplePath = Environment.GetEnvironmentVariable("SHARPCOMPRESS_XZ_PROFILE_FILE");
            // liblzma-generated x86+LZMA2 fixture: 1 MiB with two branch instructions per 16 bytes,
            // a 4 KiB dictionary, and a CRC64 check. No external tool or download is needed to run it.
            inputBytes = string.IsNullOrEmpty(samplePath)
                ? Convert.FromBase64String(
                    "/Td6WFoAAATm1rRGAgEEACEBAADaM60D7///AOhdAEhjnAGAEAIyiwApUKg0kYm2/bQYSf9EWlfsb/i+j5uibr+aPm+yv9LOWu9UeBeuse7x5AJh1Nt+koa2RqIOOFj4Zc2Juo2wZQ6Zcpqj44zFDA6XZPOySWdZ0GSN+ICWuXBwsOQ2cJWYin4qkfIndfcZwAaYTZj92K/VkA/EJVP49ZE2MQWlsO5vwXBNRwzRkRGqrWAdus6xJxhcWYbpZlJYvul2rFnk5VsFCPnH2q38+1IrdM0eWyBC+d1TPfgpZAk7gMsqbN+1O/DEvS5fqg8+S2ZCkBMO/xCT+HF4WfgLzf+U3vY5IgAAghdBqYf1VGgAAYQCgIBAAM0zocCxxGf7AgAAAAAEWVo="
                )
                : File.ReadAllBytes(samplePath);
        }
    }

    private Stream CreateStream(Stream input)
    {
        if (Layer == "Xz")
        {
            return new XZStream(input);
        }
        BlockFilter filter = Layer switch
        {
            "X86" => new X86Filter(),
            "ARM" => new ArmFilter(),
            "ARMThumb" => new ArmThumbFilter(),
            "PowerPC" => new PowerPCFilter(),
            "SPARC" => new SparcFilter(),
            "IA64" => new IA64Filter(),
            _ => throw new InvalidOperationException("Unknown BCJ architecture"),
        };
        filter.SetBaseStream(input);
        return filter;
    }

    [Benchmark]
    public long Read()
    {
        using var input = new MemoryStream(inputBytes, writable: false);
        using var stream = CreateStream(input);
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 5, ReadSize)) != 0)
        {
            total += read;
        }
        return total;
    }

    [Benchmark]
    public async Task<long> ReadAsyncArray()
    {
        using var input = new MemoryStream(inputBytes, writable: false);
        using var stream = CreateStream(input);
        long total = 0;
        int read;
        while (
            (
                read = await stream
                    .ReadAsync(buffer, 5, ReadSize, CancellationToken.None)
                    .ConfigureAwait(false)
            ) != 0
        )
        {
            total += read;
        }
        return total;
    }

    [Benchmark]
    public async Task<long> ReadAsyncMemory()
    {
        using var input = new MemoryStream(inputBytes, writable: false);
        using var stream = CreateStream(input);
        long total = 0;
        int read;
        while (
            (
                read = await stream
                    .ReadAsync(buffer.AsMemory(5, ReadSize), CancellationToken.None)
                    .ConfigureAwait(false)
            ) != 0
        )
        {
            total += read;
        }
        return total;
    }
}
