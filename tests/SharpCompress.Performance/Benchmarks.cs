using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class Benchmarks
{
    string filename = $"/Users/adam/Downloads/original.7z";

    [Benchmark]
    public void SharpCompress_0_44_Original()
    {
         Extractor.GetFiles(filename);
    }
}
