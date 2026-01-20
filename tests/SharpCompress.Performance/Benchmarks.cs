using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class Benchmarks
{
    string filename = $"/Users/adam/Downloads/original.7z";

    [Benchmark]
    public async Task SharpCompress_0_44_Original()
    {
        await Extractor.GetFiles(filename);
    }
}
