using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class Benchmarks
{
    private
        string filename = @"C:\Users\adh\Downloads\7Zip Samples-20260119T114757Z-3-001\7Zip Samples\original.7z";

    [Benchmark]
    public async Task SharpCompress_0_44_Original()
    {
        await Extractor.GetFiles(filename);
    }
}
