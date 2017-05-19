using System;
using System.IO;
using CommandLine;
using SharpCompress.Archives;

namespace SharpCompress
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<InfoOptions, ExtractOptions>(args)
                              .MapResult(
                                         (InfoOptions opts) => Info(opts),
                                         (ExtractOptions opts) => Extract(opts),
                                         errs => 1);
        }

        public static int Info(InfoOptions options)
        {
            foreach (var s in options.Path)
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

                            if (options.ShowEntries)
                            {
                                foreach (var archiveEntry in archive.Entries)
                                {
                                    Console.WriteLine($"\tEntry: {archiveEntry.Key}");
                                }
                            }
                        }
                    }
                    catch (Exception e)
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
        
        public static int Extract(ExtractOptions options)
        {
            return 0;
        }
    }
}