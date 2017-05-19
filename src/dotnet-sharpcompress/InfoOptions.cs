using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using SharpCompress.Archives;

namespace SharpCompress
{
    [Verb("i", HelpText = "Information about an archive")]
    public class InfoOptions
    {
        [Value(0, Min = 1)]
        public IEnumerable<string> Path { get; set; }   
        
        [Option('e',  HelpText = "Show Archive Entry Information")]
        public bool ShowEntries { get; set; }
        
        public int Process()
        {
            foreach (var s in Path)
            {
                if (File.Exists(s))
                {
                    Console.WriteLine($"=== Archive: {s}");
                    try
                    {
                        using (var archive = ArchiveFactory.Open(File.OpenRead(s)))
                        {
                            Console.WriteLine($"Archive Type: {archive.Type}");

                            Console.WriteLine($"Size: {archive.TotalSize}");
                            Console.WriteLine($"Uncompressed Size: {archive.TotalUncompressSize}");

                            if (ShowEntries)
                            {
                                foreach (var archiveEntry in archive.Entries)
                                {
                                    Console.WriteLine($"\tEntry: {archiveEntry.Key}");
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Archive Type is unknown.");
                    }
                }
                else
                {
                    Console.WriteLine($"{s} does not exist");
                    return 1;
                }
            }
            return 0;
        }
    }
}