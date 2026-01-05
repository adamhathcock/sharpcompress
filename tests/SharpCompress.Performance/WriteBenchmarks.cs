using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace SharpCompress.Performance;

/// <summary>
/// Benchmarks for Writer operations.
/// Tests creating archives with different compression formats using forward-only Writer API.
/// </summary>
[MemoryDiagnoser]
public class WriteBenchmarks : BenchmarkBase
{
    private string _tempOutputPath = null!;
    private readonly string[] _testFiles = null!;

    public WriteBenchmarks()
    {
        // Get some test files to compress
        var originalPath = Path.Combine(Path.GetDirectoryName(TEST_ARCHIVES_PATH)!, "Original");
        if (Directory.Exists(originalPath))
        {
            _testFiles = Directory.GetFiles(originalPath).Take(5).ToArray();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _tempOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempOutputPath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempOutputPath))
        {
            Directory.Delete(_tempOutputPath, true);
        }
    }

    [Benchmark]
    public void ZipWriterWrite()
    {
        if (_testFiles == null || _testFiles.Length == 0)
            return;

        var outputFile = Path.Combine(_tempOutputPath, "test.zip");
        using var stream = File.Create(outputFile);
        using var writer = WriterFactory.Open(
            stream,
            ArchiveType.Zip,
            new WriterOptions(CompressionType.Deflate)
        );
        foreach (var file in _testFiles)
        {
            writer.Write(Path.GetFileName(file), file);
        }
    }

    [Benchmark]
    public void TarWriterWrite()
    {
        if (_testFiles == null || _testFiles.Length == 0)
            return;

        var outputFile = Path.Combine(_tempOutputPath, "test.tar");
        using var stream = File.Create(outputFile);
        using var writer = WriterFactory.Open(
            stream,
            ArchiveType.Tar,
            new WriterOptions(CompressionType.None)
        );
        foreach (var file in _testFiles)
        {
            writer.Write(Path.GetFileName(file), file);
        }
    }

    [Benchmark]
    public void TarGzWriterWrite()
    {
        if (_testFiles == null || _testFiles.Length == 0)
            return;

        var outputFile = Path.Combine(_tempOutputPath, "test.tar.gz");
        using var stream = File.Create(outputFile);
        using var writer = WriterFactory.Open(
            stream,
            ArchiveType.Tar,
            new WriterOptions(CompressionType.GZip)
        );
        foreach (var file in _testFiles)
        {
            writer.Write(Path.GetFileName(file), file);
        }
    }
}
