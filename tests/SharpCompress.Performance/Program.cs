using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using SharpCompress.Archives;
using SharpCompress.Performance;
using SharpCompress.Readers;
using SharpCompress.Test;
/*
var index = AppDomain.CurrentDomain.BaseDirectory.IndexOf(
    "SharpCompress.Performance",
    StringComparison.OrdinalIgnoreCase
);
var path = AppDomain.CurrentDomain.BaseDirectory.Substring(0, index);
var SOLUTION_BASE_PATH = Path.GetDirectoryName(path) ?? throw new ArgumentNullException();

var TEST_ARCHIVES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Archives");

//using var _ = JetbrainsProfiler.Memory($"/Users/adam/git/temp");
using (var __ = JetbrainsProfiler.Cpu($"/Users/adam/git/temp"))
{
    string filename = $"/Users/adam/Downloads/original.7z";
    for (int i = 0; i < 10; i++)
    {
         Extractor.GetFiles(filename);
        Console.WriteLine(i);
    }

    Console.WriteLine("Still running...");
}
await Task.Delay(500);*/

BenchmarkRunner.Run<Benchmarks>();
