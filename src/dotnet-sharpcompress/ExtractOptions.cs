using System;
using CommandLine;
using SharpCompress.Readers;

namespace SharpCompress
{
    [Verb("x", HelpText = "Extract an archive")]
    public class ExtractOptions : BaseOptions
    {

        [Option('p', HelpText = "Path to extract to")]
        public string ExtractionPath { get; set; } = AppContext.BaseDirectory;
        
        public int Process()
        {
            foreach (var fileInfo in GetFilesFromPath())
            {
                Console.WriteLine($"Extracting archive {fileInfo.FullName} to path: {ExtractionPath}");
                using (var reader = ReaderFactory.Open(fileInfo.OpenRead()))
                {
                    while (reader.MoveToNextEntry())
                    {
                        var progress = new ProgressBar();
                            reader.EntryExtractionProgress += (sender, args) =>
                                                              {
                                                                  progress.Report(args.ReaderProgress.PercentageReadExact);
                                                              };
                            Console.Write($"Extracting entry {reader.Entry.Key}: ");
                            reader.WriteEntryToDirectory(ExtractionPath);
                            Console.WriteLine();
                    }
                }
            }
            return 1;
        }
    }
}