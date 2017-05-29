using System;
using CommandLine;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace SharpCompress
{
    [Verb("i", HelpText = "Information about an archive")]
    public class InfoOptions : BaseOptions
    {
        [Option('e',  HelpText = "Show Archive Entry Information")]
        public bool ShowEntries { get; set; }
        
        public int Process()
        {
            foreach (var fileInfo in GetFilesFromPath())
            {
                Console.WriteLine($"=== Archive: {fileInfo}");
                try
                {
                    using (var archive = ArchiveFactory.Open(fileInfo.OpenRead()))
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
                catch (InvalidFormatException)
                {
                    using (ConsoleHelper.PushError())
                    {
                        Console.WriteLine("Archive Type is unknown.");
                    }
                }
                catch (Exception e)
                {
                    using (ConsoleHelper.PushError())
                    {
                        Console.WriteLine($"Unhandled Error: {e}");
                        return 1;
                    }
                }
            }
            return 0;
        }
    }
}