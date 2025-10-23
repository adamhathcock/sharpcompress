using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Performance;
using SharpCompress.Readers;
using SharpCompress.Test;

var index = AppDomain.CurrentDomain.BaseDirectory.IndexOf(
    "SharpCompress.Performance",
    StringComparison.OrdinalIgnoreCase
);
var path = AppDomain.CurrentDomain.BaseDirectory.Substring(0, index);
var SOLUTION_BASE_PATH = Path.GetDirectoryName(path) ?? throw new ArgumentNullException();

var TEST_ARCHIVES_PATH = Path.Combine(SOLUTION_BASE_PATH, "TestArchives", "Archives");

//using var _ = JetbrainsProfiler.Memory($"/Users/adam/temp/");
using (var __ = JetbrainsProfiler.Cpu($"/Users/adam/temp/"))
{
    var testArchives = new[]
    {
        "Rar.Audio_program.rar",

        //"64bitstream.zip.7z",
        //"TarWithSymlink.tar.gz"
    };
    var arcs = testArchives.Select(a => Path.Combine(TEST_ARCHIVES_PATH, a)).ToArray();

    for (int i = 0; i < 50; i++)
    {
        using var found = ArchiveFactory.Open(arcs[0]);
        foreach (var entry in found.Entries.Where(entry => !entry.IsDirectory))
        {
            Console.WriteLine($"Extracting {entry.Key}");
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
        /*using var found = ReaderFactory.Open(arcs[0]);
        while (found.MoveToNextEntry())
        {
            var entry = found.Entry;
            if (entry.IsDirectory)
                continue;

            Console.WriteLine($"Extracting {entry.Key}");
            found.WriteEntryTo(Stream.Null);
        }*/
    }

    Console.WriteLine("Still running...");
}
await Task.Delay(500);
