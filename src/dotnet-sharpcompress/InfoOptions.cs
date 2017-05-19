using System.Collections.Generic;
using CommandLine;

namespace SharpCompress
{
    [Verb("i", HelpText = "Information about an archive")]
    public class InfoOptions
    {
        [Value(0, Min = 1)]
        public IEnumerable<string> Path { get; set; }   
        
        [Option('e',  HelpText = "Show Archive Entry Information")]
        public bool ShowEntries { get; set; }
    }
}